using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ticket.Models;
using Ticket.Services;

namespace Ticket.ViewModels
{
    public partial class ScannerViewModel : BaseViewModel
    {
        private readonly ITicketValidationService _validationService;
        private readonly IQrCodeService _qrService;
        private readonly IAuthService _authService;
        private readonly IRoleService _roleService;

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private bool _isTorchOn;

        public ScannerViewModel(ITicketValidationService validationService, IQrCodeService qrService, IAuthService authService, IRoleService roleService)
        {
            _validationService = validationService;
            _qrService = qrService;
            _authService = authService;
            _roleService = roleService;
            Title = "Scanner";
        }

        public async Task OnQrDetected(string barcodeValue)
        {
            if (!IsScanning) return;

            IsScanning = false;

            try
            {
                // Check user role - only Operator and Admin can scan tickets
                var userRole = await _authService.GetCurrentUserRoleAsync();
                if (!userRole.HasValue || !_roleService.HasRole(userRole.Value, UserRole.Operator))
                {
                    await PopupHelper.ShowWarningToastAsync("You don't have permission to scan tickets.");
                    await ResetScannerAsync();
                    return;
                }
                var payload = _qrService.ParseQrPayload(barcodeValue);
                if (payload is null)
                {
                    await PopupHelper.ShowIconTextAsync("\uf057", "Invalid QR Code", "The scanned QR code is not a valid event ticket.", "#EF4444", "Scan Again");
                    return;
                }

                var result = await _validationService.ValidateTicketAsync(payload.TicketId, payload.Token);

                switch (result.Status)
                {
                    case ValidationResultStatus.Valid:
                        var notes = !string.IsNullOrEmpty(result.Attendee?.Notes) ? $"\n{result.Attendee.Notes}" : "";
await PopupHelper.ShowIconTextAsync("\uf058", "Access Granted", $"{result.Attendee?.FullName}\n{result.Attendee?.TicketType}{notes}\n\n{result.Message}", "#10B981", "Scan Again");
                        break;
                    case ValidationResultStatus.AlreadyUsed:
                        await PopupHelper.ShowIconTextAsync("\uf071", "Already Checked In", $"{result.Attendee?.FullName}\n{result.Attendee?.TicketType}\n\n{result.Message}", "#F59E0B", "Scan Again");
                        break;
                    case ValidationResultStatus.Invalid:
                        await PopupHelper.ShowIconTextAsync("\uf057", "Invalid Ticket", result.Message, "#EF4444", "Scan Again");
                        break;
                }
            }
            catch (Exception ex)
            {
                await PopupHelper.ShowIconTextAsync("\uf057", "Error", ex.Message, "#EF4444", "Scan Again");
            }
            finally
            {
                await ResetScannerAsync();
            }
        }

        [RelayCommand]
        private async Task ResetScannerAsync()
        {
            await Task.Delay(800);
            IsScanning = true;
        }

        [RelayCommand]
        private void ToggleTorch()
        {
            IsTorchOn = !IsTorchOn;
        }
    }
}
