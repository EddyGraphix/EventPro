using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ticket.Helpers;
using Ticket.Models;
using Ticket.Services;

namespace Ticket.ViewModels
{
    [QueryProperty(nameof(ScannedTicketCode), "ticketCode")]
    public partial class RegisterParticipantViewModel : BaseViewModel
    {
        private readonly ISupabaseClient _supabase;
        private readonly IQrCodeService _qrService;
        private readonly IPhotoUploadService _photoUploader;
        private readonly IPhotoCompressionService _photoCompressor;
        private readonly IAuthService _authService;
        private readonly IRoleService _roleService;

        [ObservableProperty]
        private string _fullName = string.Empty;

        [ObservableProperty]
        private string _phoneNumber = string.Empty;

        [ObservableProperty]
        private string _ticketType = "General";

        [ObservableProperty]
        private string _paymentStatus = "Pending";

        [ObservableProperty]
        private string _notes = string.Empty;

        [ObservableProperty]
        private string _scannedTicketCode = string.Empty;

        [ObservableProperty]
        private ImageSource? _photoPreview;

        private string? _photoPath;

        public List<string> TicketTypes { get; } = new() { "General", "VIP", "Premium", "Early Bird", "Student" };
        public List<string> PaymentStatuses { get; } = new() { "Pending", "Paid", "Free" };

        public RegisterParticipantViewModel(ISupabaseClient supabase, IQrCodeService qrService, IPhotoUploadService photoUploader, IPhotoCompressionService photoCompressor, IAuthService authService, IRoleService roleService)
        {
            _supabase = supabase;
            _qrService = qrService;
            _photoUploader = photoUploader;
            _photoCompressor = photoCompressor;
            _authService = authService;
            _roleService = roleService;
            Title = "Register";
        }

        [RelayCommand]
        private void ResetForm()
        {
            FullName = string.Empty;
            PhoneNumber = string.Empty;
            TicketType = "General";
            PaymentStatus = "Pending";
            Notes = string.Empty;
            ScannedTicketCode = string.Empty;
            PhotoPreview = null;
            _photoPath = null;
        }

        [RelayCommand]
        private async Task TakePhotoAsync()
        {
            try
            {
                if (!MediaPicker.IsCaptureSupported)
                {
                    await PopupHelper.ShowWarningToastAsync("Camera not available on this device.");
                    return;
                }

                var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (cameraStatus != PermissionStatus.Granted)
                {
                    cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                    if (cameraStatus != PermissionStatus.Granted)
                    {
                        await PopupHelper.ShowWarningToastAsync("Camera permission is required to take photos.");
                        return;
                    }
                }

#if ANDROID
                if (!OperatingSystem.IsAndroidVersionAtLeast(29))
                {
                    var storageStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                    if (storageStatus != PermissionStatus.Granted)
                    {
                        storageStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
                        if (storageStatus != PermissionStatus.Granted)
                        {
                            await PopupHelper.ShowWarningToastAsync("Storage permission is required to save photos.");
                            return;
                        }
                    }
                }
#endif

                var photo = await MediaPicker.CapturePhotoAsync();
                if (photo is null) return;

                _photoPath = await SaveToCacheAsync(photo);
            }
            catch (Exception ex)
            {
                await PopupHelper.ShowErrorToastAsync(ex.Message);
            }
        }

        [RelayCommand]
        private async Task PickPhotoAsync()
        {
            try
            {
#pragma warning disable CS0618
                var photo = await MediaPicker.PickPhotoAsync();
#pragma warning restore CS0618
                if (photo is null) return;

                _photoPath = await SaveToCacheAsync(photo);
            }
            catch (Exception ex)
            {
                await PopupHelper.ShowErrorToastAsync(ex.Message);
            }
        }

        private static async Task<string> SaveToCacheAsync(FileResult photo)
        {
            var dir = Path.Combine(FileSystem.CacheDirectory, "photos");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"photo_{DateTime.UtcNow:yyyyMMddHHmmssfff}.jpg");
            using var stream = await photo.OpenReadAsync();
            using var fs = File.Create(path);
            await stream.CopyToAsync(fs);
            return path;
        }

        [RelayCommand]
        private void RemovePhoto()
        {
            PhotoPreview = null;
            _photoPath = null;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            var userRole = await _authService.GetCurrentUserRoleAsync();
            if (!userRole.HasValue || !_roleService.HasRole(userRole.Value, UserRole.Operator))
            {
                await PopupHelper.ShowWarningToastAsync("You don't have permission to register attendees.");
                return;
            }

            if (string.IsNullOrWhiteSpace(FullName))
            {
                await PopupHelper.ShowWarningToastAsync("Full name is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(PhoneNumber))
            {
                await PopupHelper.ShowWarningToastAsync("Phone number is required.");
                return;
            }

            IsBusy = true;
            try
            {
                var existing = await _supabase.GetAttendeeByPhoneAsync(PhoneNumber.Trim());
                if (existing is not null)
                {
                    await PopupHelper.ShowWarningToastAsync("An attendee with this phone number already exists.");
                    return;
                }

                var attendee = new Attendee
                {
                    FullName = FullName.Trim(),
                    PhoneNumber = PhoneNumber.Trim(),
                    TicketType = TicketType,
                    PaymentStatus = PaymentStatus,
                    Notes = Notes?.Trim(),
                    TicketCode = TicketCodeGenerator.GenerateCode(),
                    QrToken = TicketCodeGenerator.GenerateToken(),
                    RegisteredAt = DateTime.UtcNow
                };

                await _supabase.SaveAttendeeAsync(attendee);

                if (!string.IsNullOrEmpty(_photoPath))
                {
                    var fileName = $"{attendee.TicketCode}_{DateTime.UtcNow:yyyyMMddHHmmss}.jpg";

                    var compressedPath = await _photoCompressor.CompressPhotoAsync(_photoPath, fileName, maxWidth: 1024, maxHeight: 1024, quality: 80);
                    if (compressedPath != null)
                    {
                        var originalSize = await _photoCompressor.GetFileSizeAsync(_photoPath);
                        var compressedSize = await _photoCompressor.GetFileSizeAsync(compressedPath);
                        System.Diagnostics.Debug.WriteLine($"Photo compressed: {originalSize / 1024}KB -> {compressedSize / 1024}KB");

                        var remote = await _photoUploader.UploadNowOrEnqueueAsync(compressedPath, fileName, attendee.Id, attendee.TicketCode);
                        if (!string.IsNullOrEmpty(remote))
                        {
                            attendee.PhotoUrl = remote;
                            await _supabase.SaveAttendeeAsync(attendee);
                        }
                    }
                    else
                    {
                        var remote = await _photoUploader.UploadNowOrEnqueueAsync(_photoPath, fileName, attendee.Id, attendee.TicketCode);
                        if (!string.IsNullOrEmpty(remote))
                        {
                            attendee.PhotoUrl = remote;
                            await _supabase.SaveAttendeeAsync(attendee);
                        }
                    }
                }

                await Shell.Current.GoToAsync($"ticketPreview?attendeeId={attendee.Id}");

                FullName = string.Empty;
                PhoneNumber = string.Empty;
                TicketType = "General";
                PaymentStatus = "Pending";
                Notes = string.Empty;
                PhotoPreview = null;
                _photoPath = null;
            }
            catch (Exception ex)
            {
                await PopupHelper.ShowErrorToastAsync(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
