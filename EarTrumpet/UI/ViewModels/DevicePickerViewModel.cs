using EarTrumpet.UI.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace EarTrumpet.UI.ViewModels
{
    /// <summary>
    /// In-flyout list of playback devices. Clicking one makes it the default output device.
    /// </summary>
    public class DevicePickerItemViewModel
    {
        public string DisplayName { get; set; }
        public bool IsDefault { get; set; }
        public System.Windows.Input.ICommand Command { get; set; }
    }

    class DevicePickerViewModel : IFocusedViewModel
    {
        public event Action RequestClose;

        public string DisplayName => "Output device";
        public ObservableCollection<ToolbarItemViewModel> Toolbar { get; }
        public ObservableCollection<DevicePickerItemViewModel> Devices { get; }

        public DevicePickerViewModel(DeviceCollectionViewModel mainViewModel)
        {
            Toolbar = new ObservableCollection<ToolbarItemViewModel>
            {
                new ToolbarItemViewModel
                {
                    GlyphFontSize = 10,
                    DisplayName = Properties.Resources.CloseButtonAccessibleText,
                    Glyph = "",
                    Command = new RelayCommand(() => RequestClose?.Invoke())
                }
            };

            Devices = new ObservableCollection<DevicePickerItemViewModel>(
                mainViewModel.AllDevices.Select(device => new DevicePickerItemViewModel
                {
                    DisplayName = device.DisplayName,
                    IsDefault = mainViewModel.Default?.Id == device.Id,
                    Command = new RelayCommand(() =>
                    {
                        device.MakeDefaultDevice();
                        RequestClose?.Invoke();
                    })
                }));
        }

        public void Closing() { }
    }
}
