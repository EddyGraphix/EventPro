using System.Windows.Input;
using UXDivers.Popups.Services;
using UXDivers.Popups.Maui.Controls;

namespace Ticket.Services
{
    public static class PopupHelper
    {
        public static async Task ShowToastAsync(string title, Color? iconColor = null, int durationMs = 2000)
        {
            var toast = new Toast
            {
                Title = title,
                IconColor = iconColor ?? Color.FromArgb("#1F2937")
            };
            await IPopupService.Current.PushAsync(toast, waitUntilClosed: false);
            await Task.Delay(durationMs);
            await IPopupService.Current.PopAsync(toast);
        }

        public static async Task ShowErrorToastAsync(string message)
        {
            await ShowToastAsync(message, Color.FromArgb("#EF4444"), 3000);
        }

        public static async Task ShowSuccessToastAsync(string message)
        {
            await ShowToastAsync(message, Color.FromArgb("#10B981"), 2000);
        }

        public static async Task ShowWarningToastAsync(string message)
        {
            await ShowToastAsync(message, Color.FromArgb("#F59E0B"), 2500);
        }

        public static async Task<bool> ShowConfirmAsync(string title, string message, string confirmText, string cancelText)
        {
            var tcs = new TaskCompletionSource<bool>();
            var popup = new SimpleActionPopup
            {
                Title = title,
                Text = message,
                ActionButtonText = confirmText,
                SecondaryActionButtonText = cancelText,
                ActionButtonCommand = new Command(async () =>
                {
                    try { await IPopupService.Current.PopAsync(); } catch { }
                    tcs.TrySetResult(true);
                }),
                SecondaryActionButtonCommand = new Command(async () =>
                {
                    try { await IPopupService.Current.PopAsync(); } catch { }
                    tcs.TrySetResult(false);
                })
            };
            await IPopupService.Current.PushAsync(popup);
            return await tcs.Task;
        }

        public static async Task ShowIconTextAsync(string icon, string title, string text, string iconColorHex, string buttonText = "OK")
        {
            var popup = new IconTextPopup
            {
                IconText = icon,
                Title = title,
                Text = text,
                IconColor = Color.FromArgb(iconColorHex),
                ActionButtonText = buttonText
            };
            await IPopupService.Current.PushAsync(popup);
        }
    }
}
