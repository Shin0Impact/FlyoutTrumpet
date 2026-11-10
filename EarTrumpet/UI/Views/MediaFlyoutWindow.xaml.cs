using EarTrumpet.Extensions;
using EarTrumpet.Interop;
using EarTrumpet.UI.Helpers;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EarTrumpet.UI.Views
{
    /// <summary>
    /// Windows 10 style "now playing" flyout shown in the top-left corner of the screen.
    /// It never takes focus, hides itself after a few seconds and stays while the mouse is over it.
    /// </summary>
    public partial class MediaFlyoutWindow : Window
    {
        private static readonly TimeSpan VisibleDuration = TimeSpan.FromSeconds(2.5);

        private readonly MediaSessionWatcher _watcher;
        private readonly DispatcherTimer _hideTimer;
        private readonly DispatcherTimer _positionTimer;
        private MediaSnapshot _snapshot;
        private bool _isShown;
        private ImageSource _shownThumbnail;

        public MediaFlyoutWindow(MediaSessionWatcher watcher)
        {
            InitializeComponent();

            _watcher = watcher;
            _watcher.Changed += OnMediaChanged;

            _hideTimer = new DispatcherTimer { Interval = VisibleDuration };
            _hideTimer.Tick += (_, __) => HideFlyout();
            _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
            _positionTimer.Tick += (_, __) => UpdateTimeline();

            MouseEnter += (_, __) => _hideTimer.Stop();
            MouseLeave += (_, __) => { if (_isShown) _hideTimer.Start(); };

            SourceInitialized += (_, __) =>
            {
                // Tool window (no Alt+Tab entry) that never steals focus from what you're doing.
                this.ApplyExtendedWindowStyle(User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE);
                this.EnableRoundedCornersIfApplicable();
            };
        }

        private void OnMediaChanged(MediaSnapshot snapshot, bool show)
        {
            _snapshot = snapshot;
            if (snapshot == null)
            {
                HideFlyout();
                return;
            }

            TitleText.Text = snapshot.Title;
            ArtistText.Text = snapshot.Artist;
            PlayPauseButton.Content = snapshot.IsPlaying ? "" : "";
            PrevButton.IsEnabled = snapshot.CanPrevious;
            NextButton.IsEnabled = snapshot.CanNext;
            PlayPauseButton.IsEnabled = snapshot.CanPlayPause;
            if (!ReferenceEquals(snapshot.Thumbnail, _shownThumbnail))
            {
                _shownThumbnail = snapshot.Thumbnail;
                ThumbnailBorder.Background = _shownThumbnail == null ? null :
                    new ImageBrush(_shownThumbnail) { Stretch = Stretch.UniformToFill };
            }
            DurationText.Text = Format(snapshot.Duration);
            UpdateTimeline();

            // Only a real event (new track, play/pause, new app) shows it; plain updates must never extend it.
            if (show)
            {
                ShowFlyout();
            }
        }

        /// <summary>Shows the flyout for the media that is currently known (e.g. after a volume change).</summary>
        public void ShowIfMedia()
        {
            if (_snapshot != null && !string.IsNullOrWhiteSpace(_snapshot.Title))
            {
                ShowFlyout();
            }
        }

        private void ShowFlyout()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Left + 16;
            Top = workArea.Top + 16;

            if (!_isShown)
            {
                _isShown = true;
                Show();
                _positionTimer.Start();
            }

            _hideTimer.Stop();
            if (!IsMouseOver) _hideTimer.Start();
        }

        private void HideFlyout()
        {
            _hideTimer.Stop();
            if (!_isShown) return;

            _isShown = false;
            _positionTimer.Stop();
            Hide();
        }

        private void UpdateTimeline()
        {
            if (_snapshot == null) return;

            var position = _snapshot.GetPosition();
            PositionText.Text = Format(position);
            TimelineBar.Value = _snapshot.Duration > TimeSpan.Zero ? position.TotalSeconds / _snapshot.Duration.TotalSeconds : 0;
        }

        private static string Format(TimeSpan time)
        {
            return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e) => _watcher.Previous();
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e) => _watcher.PlayPause();
        private void NextButton_Click(object sender, RoutedEventArgs e) => _watcher.Next();
    }
}
