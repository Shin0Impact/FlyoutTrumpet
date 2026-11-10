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
        private bool _wasPlaying;
        private ImageSource _thumbnail;

        public async void Start()
        {
            try
            {
                _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                _manager.CurrentSessionChanged += (_, __) => _dispatcher.BeginInvoke((Action)AttachToCurrentSession);
                AttachToCurrentSession();
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

        private void AttachToCurrentSession()
        {
            if (_session != null)
            {
                _session.MediaPropertiesChanged -= OnSessionChanged;
                _session.PlaybackInfoChanged -= OnSessionChanged;
                _session.TimelinePropertiesChanged -= OnSessionChanged;
            }

            _session = _manager?.GetCurrentSession();
            _trackKey = null;

            if (_session != null)
            {
                _session.MediaPropertiesChanged += OnSessionChanged;
                _session.PlaybackInfoChanged += OnSessionChanged;
                _session.TimelinePropertiesChanged += OnSessionChanged;
            }
            Refresh(false);
        }

        // WinRT raises these on a worker thread.
        private void OnSessionChanged(GlobalSystemMediaTransportControlsSession sender, object args)
        {
            _dispatcher.BeginInvoke((Action)(() => Refresh(true)));
        }

        private async void Refresh(bool allowShow)
        {
            try
            {
                var session = _session;
                if (session == null)
                {
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
                    _thumbnail = await LoadThumbnailAsync(props.Thumbnail);
                    if (session != _session) return;
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
                };
                snapshot.SetPosition(timeline.Position - timeline.StartTime, timeline.LastUpdatedTime);

                var show = allowShow && !string.IsNullOrWhiteSpace(snapshot.Title) &&
                           (trackChanged || snapshot.IsPlaying != _wasPlaying);
                _wasPlaying = snapshot.IsPlaying;
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
