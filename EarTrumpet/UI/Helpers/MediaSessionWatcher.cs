using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;

namespace EarTrumpet.UI.Helpers
{
    public class MediaSnapshot
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string AppId { get; set; }
        public bool IsPlaying { get; set; }
        public bool CanPrevious { get; set; }
        public bool CanNext { get; set; }
        public bool CanPlayPause { get; set; }
        public ImageSource Thumbnail { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan StartTime { get; set; }
        public bool CanSeek { get; set; }

        private TimeSpan _position;
        private DateTimeOffset _positionTime;

        public void SetPosition(TimeSpan position, DateTimeOffset reportedAt)
        {
            _position = position;
            _positionTime = reportedAt;
        }

        // Players only report the position now and then, so extrapolate while playing.
        public TimeSpan GetPosition()
        {
            var pos = _position;
            if (IsPlaying)
            {
                pos += DateTimeOffset.Now - _positionTime;
            }
            if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;
            if (Duration > TimeSpan.Zero && pos > Duration) pos = Duration;
            return pos;
        }
    }

    /// <summary>
    /// Watches the Windows System Media Transport Controls (SMTC) - the same API the Windows 10 media flyout used -
    /// so any player that integrates with it (Spotify, browsers, WhatsApp voice notes, ...) shows up here.
    /// </summary>
    public class MediaSessionWatcher
    {
        // snapshot (null if nothing is playing), and whether this change is worth popping the flyout for.
        public event Action<MediaSnapshot, bool> Changed;

        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        private GlobalSystemMediaTransportControlsSessionManager _manager;
        private GlobalSystemMediaTransportControlsSession _session;
        private string _trackKey;
        private ImageSource _thumbnail;
        private MediaSnapshot _snapshot;

        public async void Start()
        {
            try
            {
                _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                // A different app starting media makes it the current session: that should pop the flyout.
                _manager.CurrentSessionChanged += (_, __) => _dispatcher.BeginInvoke((Action)(() => AttachToCurrentSession(true)));
                AttachToCurrentSession(false);
            }
            catch (Exception ex)
            {
                // Windows older than 1809, or SMTC unavailable: the flyout simply never appears.
                Trace.WriteLine($"MediaSessionWatcher Start: {ex}");
            }
        }

        public void PlayPause() => _session?.TryTogglePlayPauseAsync();
        public void Next() => _session?.TrySkipNextAsync();
        public void Previous() => _session?.TrySkipPreviousAsync();

        public void Seek(TimeSpan position)
        {
            var snapshot = _snapshot;
            if (_session == null || snapshot == null) return;

            _session.TryChangePlaybackPositionAsync((snapshot.StartTime + position).Ticks);
            snapshot.SetPosition(position, DateTimeOffset.Now);
        }

        private void AttachToCurrentSession(bool show)
        {
            if (_session != null)
            {
                _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _session.PlaybackInfoChanged -= OnSessionChanged;
                _session.TimelinePropertiesChanged -= OnTimelineChanged;
            }

            _session = _manager?.GetCurrentSession();
            _trackKey = null;

            if (_session != null)
            {
                _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
                _session.PlaybackInfoChanged += OnSessionChanged;
                _session.TimelinePropertiesChanged += OnTimelineChanged;
            }
            Refresh(show);
        }

        // WinRT raises these on a worker thread.
        private void OnSessionChanged(GlobalSystemMediaTransportControlsSession sender, object args)
        {
            _dispatcher.BeginInvoke((Action)(() => Refresh(true)));
        }

        // Title/art changed. Browsers often deliver the new cover after the title, so re-read the art every time.
        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, object args)
        {
            _dispatcher.BeginInvoke((Action)(() => Refresh(true, true)));
        }

        // Players fire this constantly while playing; just keep the position current, don't rebuild anything.
        private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession sender, object args)
        {
            _dispatcher.BeginInvoke((Action)(() =>
            {
                var snapshot = _snapshot;
                if (snapshot == null || sender != _session) return;
                try
                {
                    var timeline = sender.GetTimelineProperties();
                    snapshot.Duration = timeline.EndTime - timeline.StartTime;
                    snapshot.SetPosition(timeline.Position - timeline.StartTime, timeline.LastUpdatedTime);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"MediaSessionWatcher OnTimelineChanged: {ex.Message}");
                }
            }));
        }

        private async void Refresh(bool allowShow, bool reloadThumbnail = false)
        {
            try
            {
                var session = _session;
                if (session == null)
                {
                    _snapshot = null;
                    Changed?.Invoke(null, false);
                    return;
                }

                var props = await session.TryGetMediaPropertiesAsync();
                if (session != _session || props == null) return;

                var playback = session.GetPlaybackInfo();
                var timeline = session.GetTimelineProperties();

                var key = $"{props.Artist}|{props.Title}|{props.AlbumTitle}";
                var trackChanged = key != _trackKey;
                if (trackChanged)
                {
                    _trackKey = key;
                    _thumbnail = null;
                }
                if (trackChanged || reloadThumbnail || _thumbnail == null)
                {
                    var loaded = await LoadThumbnailAsync(props.Thumbnail);
                    if (session != _session) return;
                    if (loaded != null || trackChanged) _thumbnail = loaded;
                }

                var snapshot = new MediaSnapshot
                {
                    Title = props.Title,
                    Artist = props.Artist,
                    AppId = session.SourceAppUserModelId,
                    IsPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    CanPrevious = playback.Controls.IsPreviousEnabled,
                    CanNext = playback.Controls.IsNextEnabled,
                    CanPlayPause = playback.Controls.IsPlayPauseToggleEnabled,
                    Thumbnail = _thumbnail,
                    Duration = timeline.EndTime - timeline.StartTime,
                    StartTime = timeline.StartTime,
                    CanSeek = playback.Controls.IsPlaybackPositionEnabled,
                };
                snapshot.SetPosition(timeline.Position - timeline.StartTime, timeline.LastUpdatedTime);

                // Only a new track/video (or a different app taking over) shows the flyout - pausing or stopping must not.
                var show = allowShow && !string.IsNullOrWhiteSpace(snapshot.Title) && trackChanged;
                _snapshot = snapshot;
                Changed?.Invoke(snapshot, show);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionWatcher Refresh: {ex}");
            }
        }

        private static async Task<ImageSource> LoadThumbnailAsync(Windows.Storage.Streams.IRandomAccessStreamReference reference)
        {
            if (reference == null) return null;
            try
            {
                using (var winrtStream = await reference.OpenReadAsync())
                using (var stream = winrtStream.AsStreamForRead())
                {
                    var memory = new MemoryStream();
                    await stream.CopyToAsync(memory);
                    memory.Position = 0;

                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = memory;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaSessionWatcher LoadThumbnail: {ex.Message}");
                return null;
            }
        }
    }
}
