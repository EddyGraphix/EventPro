using SkiaSharp;
using Ticket.Models;

namespace Ticket.Services
{
    public interface ITicketImageService
    {
        Task<Stream> GenerateTicketImageAsync(Attendee attendee);
        Task<string> SaveTicketImageAsync(Attendee attendee);
    }

    public class TicketImageService : ITicketImageService
    {
        private readonly IQrCodeService _qrService;
        private readonly ISupabaseClient _supabase;
        private static readonly HttpClient _photoClient = new() { Timeout = TimeSpan.FromSeconds(10) };

        public TicketImageService(IQrCodeService qrService, ISupabaseClient supabase)
        {
            _qrService = qrService;
            _supabase = supabase;
        }

        public async Task<Stream> GenerateTicketImageAsync(Attendee attendee)
        {
            var evt = await _supabase.GetEventAsync();
            var eventName = evt?.EventName ?? "EventPro";
            var location = evt?.Description ?? string.Empty;

            const float s = 1.8f;
            int w = (int)(800 * s), h = (int)(1450 * s);
            var info = new SKImageInfo(w, h);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            var navy = new SKColor(0x0F, 0x17, 0x2A);
            var purple = new SKColor(0x7C, 0x3A, 0xED);
            var teal = new SKColor(0x06, 0xB6, 0xD4);
            var white = SKColors.White;
            var w90 = new SKColor(0xFF, 0xFF, 0xFF, 0xE6);
            var w80 = new SKColor(0xFF, 0xFF, 0xFF, 0xCC);
            var w50 = new SKColor(0xFF, 0xFF, 0xFF, 0x80);
            var w30 = new SKColor(0xFF, 0xFF, 0xFF, 0x4D);
            var w15 = new SKColor(0xFF, 0xFF, 0xFF, 0x26);
            var w08 = new SKColor(0xFF, 0xFF, 0xFF, 0x14);
            var w04 = new SKColor(0xFF, 0xFF, 0xFF, 0x0A);

            canvas.DrawRect(0, 0, w, h, new SKPaint { Color = navy });

            var topGlow = new SKPaint
            {
                Color = new SKColor(0x7C, 0x3A, 0xED, 0x15),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 100 * s)
            };
            canvas.DrawCircle(w * 0.5f, 0, 300 * s, topGlow);

            var bottomGlow = new SKPaint
            {
                Color = new SKColor(0x06, 0xB6, 0xD4, 0x08),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 120 * s)
            };
            canvas.DrawCircle(w * 0.5f, h * 0.92f, 250 * s, bottomGlow);

            DrawDotGrid(canvas, w, h, navy, s);

            float cx = w / 2f;
            float pad = 44 * s;
            float ml = pad;
            float mr = w - pad;

            var titleFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 54 * s);
            using var subFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 16 * s);
            var nameFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 42 * s);
            using var badgeFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 18 * s);
            using var scanFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 13 * s);
            using var smallFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 12 * s);
            using var labelFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 10 * s);
            using var initFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 100 * s);
            using var brandFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 15 * s);

            // ============================================================
            // TICKET BORDER
            // ============================================================
            var borderPaint = new SKPaint
            {
                Color = new SKColor(0x7C, 0x3A, 0xED, 0x20),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1 * s
            };
            float bp = 12 * s;
            canvas.DrawRoundRect(bp, bp, w - bp * 2, h - bp * 2, 16 * s, 16 * s, borderPaint);

            DrawCornerOrnament(canvas, bp + 10 * s, bp + 10 * s, 20 * s, purple);
            DrawCornerOrnament(canvas, mr - 10 * s, bp + 10 * s, 20 * s, purple);
            DrawCornerOrnament(canvas, bp + 10 * s, h - bp - 30 * s, 20 * s, purple);
            DrawCornerOrnament(canvas, mr - 10 * s, h - bp - 30 * s, 20 * s, purple);

            float y = 108 * s;
            float maxW = mr - ml;

            // ============================================================
            // EVENT NAME
            // ============================================================
            var titleStr = eventName.ToUpper();
            var tW = titleFont.MeasureText(titleStr, new SKPaint());
            if (tW > maxW)
            {
                float fs2 = 54 * s * (maxW / tW) * 0.95f;
                titleFont.Dispose();
                titleFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), fs2);
                tW = titleFont.MeasureText(titleStr, new SKPaint());
            }

            var titleGlow = new SKPaint
            {
                Color = new SKColor(0x7C, 0x3A, 0xED, 0x20),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 16 * s)
            };
            canvas.DrawText(titleStr, cx - tW / 2f, y + 3 * s, titleFont, titleGlow);
            canvas.DrawText(titleStr, cx - tW / 2f, y, titleFont, new SKPaint { Color = white, IsAntialias = true });
            y += 46 * s;

            // ============================================================
            // LOCATION
            // ============================================================
            if (!string.IsNullOrEmpty(location))
            {
                var locW = subFont.MeasureText(location, new SKPaint());
                canvas.DrawText(location, cx - locW / 2f, y, subFont, new SKPaint { Color = w50, IsAntialias = true });
                y += 36 * s;
            }

            // ============================================================
            // GRADIENT SEPARATOR
            // ============================================================
            var sepGrad = SKShader.CreateLinearGradient(
                new SKPoint(ml, 0), new SKPoint(mr, 0),
                new[] { w04, w30, w04 }, new[] { 0f, 0.5f, 1f }, SKShaderTileMode.Clamp);
            canvas.DrawLine(ml, y, mr, y, new SKPaint { Shader = sepGrad, StrokeWidth = 1 * s, IsAntialias = true });
            y += 32 * s;

            // ============================================================
            // PERFORATED TEAR LINE
            // ============================================================
            float notchY = y;
            for (float nx_ = 0; nx_ <= w; nx_ += 30 * s)
                canvas.DrawCircle(nx_, notchY, 6 * s, new SKPaint { Color = navy, IsAntialias = true });
            canvas.DrawLine(ml, notchY, mr, notchY,
                new SKPaint { Color = w15, StrokeWidth = 1 * s, PathEffect = SKPathEffect.CreateDash(new[] { 8 * s, 8 * s }, 0), IsAntialias = true });

            var notchLabel = "━━ TICKET ━━";
            var nlW = smallFont.MeasureText(notchLabel, new SKPaint());
            canvas.DrawRoundRect(cx - nlW / 2f - 10 * s, notchY - 10 * s, nlW + 20 * s, 20 * s, 10 * s, 10 * s,
                new SKPaint { Color = navy, IsAntialias = true });
            canvas.DrawText(notchLabel, cx - nlW / 2f, notchY + 4 * s, smallFont, new SKPaint { Color = w30, IsAntialias = true });

            y += 40 * s;

            // ============================================================
            // ATTENDEE PHOTO — circular, large
            // ============================================================
            var photoBytes = await GetPhotoBytesAsync(attendee);
            float avSize = 300 * s;

            var avatarGlow = new SKPaint
            {
                Color = new SKColor(0x7C, 0x3A, 0xED, 0x15),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 10 * s)
            };
            canvas.DrawCircle(cx, y + avSize / 2f, avSize / 2f + 4 * s, avatarGlow);

            if (photoBytes is not null)
            {
                using var src = SKBitmap.Decode(photoBytes);
                if (src is not null)
                {
                    using var circ = new SKBitmap((int)avSize, (int)avSize);
                    using var cc = new SKCanvas(circ);
                    cc.Clear(SKColors.Transparent);
                    float ss = Math.Min(src.Width, src.Height);
                    float sx = (src.Width - ss) / 2f;
                    float sy = (src.Height - ss) / 2f;
                    var cp = new SKPath();
                    cp.AddCircle(avSize / 2f, avSize / 2f, avSize / 2f);
                    cc.ClipPath(cp, SKClipOperation.Intersect, true);
                    cc.DrawBitmap(src, new SKRect(-sx * avSize / ss, -sy * avSize / ss,
                        (src.Width - sx) * avSize / ss, (src.Height - sy) * avSize / ss));
                    canvas.DrawBitmap(circ, cx - avSize / 2f, y);
                }
            }
            else
            {
                var avatarGrad = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0), new SKPoint(avSize, avSize),
                    new[] { purple, teal }, new[] { 0f, 1f }, SKShaderTileMode.Clamp);
                canvas.DrawCircle(cx, y + avSize / 2f, avSize / 2f, new SKPaint { Shader = avatarGrad, IsAntialias = true });

                var initial = !string.IsNullOrEmpty(attendee.FullName) ? attendee.FullName[..1].ToUpper() : "?";
                var iw = initFont.MeasureText(initial, new SKPaint());
                canvas.DrawText(initial, cx - iw / 2f, y + avSize / 2f + 14 * s, initFont, new SKPaint { Color = white, IsAntialias = true });
            }

            y += avSize + 34 * s;

            // ============================================================
            // ATTENDEE NAME — large bold uppercase
            // ============================================================
            var an = attendee.FullName.ToUpper();
            float anW = nameFont.MeasureText(an, new SKPaint());
            if (anW > maxW)
            {
                float fs2 = 42 * s * (maxW / anW) * 0.95f;
                nameFont.Dispose();
                nameFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), fs2);
                anW = nameFont.MeasureText(an, new SKPaint());
            }
            canvas.DrawText(an, cx - anW / 2f, y + nameFont.Size * 0.85f, nameFont, new SKPaint { Color = w90, IsAntialias = true });
            y += nameFont.Size * 0.85f + 24 * s;

            // ============================================================
            // TICKET TYPE BADGE
            // ============================================================
            var tt = attendee.TicketType.ToUpper();
            if (string.IsNullOrEmpty(tt)) tt = "GENERAL";
            float ttW = badgeFont.MeasureText(tt, new SKPaint());
            float badgeH = 32 * s;
            float badgePadX = 20 * s;
            float badgeW = ttW + badgePadX * 2;

            var badgeShader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(1, 0),
                new[] { new SKColor(0x7C, 0x3A, 0xED, 0x25), new SKColor(0x25, 0x63, 0xEB, 0x12) },
                new[] { 0f, 1f }, SKShaderTileMode.Clamp);
            canvas.DrawRoundRect(cx - badgeW / 2f, y, badgeW, badgeH, 8 * s, 8 * s, new SKPaint { Shader = badgeShader, IsAntialias = true });
            canvas.DrawRoundRect(cx - badgeW / 2f, y, badgeW, badgeH, 8 * s, 8 * s,
                new SKPaint { Color = new SKColor(0x7C, 0x3A, 0xED, 0x35), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 * s });
            canvas.DrawText(tt, cx - ttW / 2f, y + badgeH / 2f + badgeFont.Size * 0.35f, badgeFont, new SKPaint { Color = white, IsAntialias = true });

            y += badgeH + 44 * s;

            // ============================================================
            // QR CODE — large, premium styling
            // ============================================================
            var qrBytes = _qrService.GenerateQrCode(attendee);
            using var qrBitmap = SKBitmap.Decode(qrBytes);
            int qrSize = (int)(290 * s);
            float qrX = cx - qrSize / 2f;
            float qrY_ = y;

            var qrOuterGlow = new SKPaint
            {
                Color = new SKColor(0x7C, 0x3A, 0xED, 0x25),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 24 * s)
            };
            canvas.DrawRoundRect(qrX - 14 * s, qrY_ - 14 * s, qrSize + 28 * s, qrSize + 28 * s, 16 * s, 16 * s, qrOuterGlow);

            var qrFramePaint = new SKPaint
            {
                Color = new SKColor(0x7C, 0x3A, 0xED, 0x08),
                IsAntialias = true,
            };
            canvas.DrawRoundRect(qrX - 12 * s, qrY_ - 12 * s, qrSize + 24 * s, qrSize + 24 * s, 14 * s, 14 * s, qrFramePaint);

            canvas.DrawRoundRect(qrX - 10 * s, qrY_ - 10 * s, qrSize + 20 * s, qrSize + 20 * s, 12 * s, 12 * s,
                new SKPaint { Color = white, IsAntialias = true });

            canvas.DrawRoundRect(qrX - 10 * s, qrY_ - 10 * s, qrSize + 20 * s, qrSize + 20 * s, 12 * s, 12 * s,
                new SKPaint { Color = new SKColor(0x7C, 0x3A, 0xED, 0x35), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * s });

            canvas.DrawBitmap(qrBitmap, new SKRect(qrX, qrY_, qrX + qrSize, qrY_ + qrSize));
            y += qrSize + 48 * s;

            var scanText = "SCAN AT EVENT ENTRANCE";
            var scanW = scanFont.MeasureText(scanText, new SKPaint());
            canvas.DrawText(scanText, cx - scanW / 2f, y, scanFont, new SKPaint { Color = w50, IsAntialias = true });

            y += 52 * s;

            // ============================================================
            // GRADIENT SEPARATOR
            // ============================================================
            canvas.DrawLine(ml, y, mr, y, new SKPaint { Shader = sepGrad, StrokeWidth = 1 * s, IsAntialias = true });
            y += 24 * s;

            // ============================================================
            // TICKET ID
            // ============================================================
            canvas.DrawText("TICKET ID", cx - labelFont.MeasureText("TICKET ID", new SKPaint()) / 2f,
                y, labelFont, new SKPaint { Color = w50, IsAntialias = true });
            y += 22 * s;

            using var tidFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 18 * s);
            var tid = $"TKT-{attendee.TicketCode}";
            var tidW = tidFont.MeasureText(tid, new SKPaint());
            canvas.DrawText(tid, cx - tidW / 2f, y + tidFont.Size * 0.75f, tidFont, new SKPaint { Color = w80, IsAntialias = true });

            y += 36 * s;

            // ============================================================
            // CONCENTRIC CYCLE LINES
            // ============================================================
            float waveCy = y + 36 * s;
            for (int i = 0; i < 8; i++)
            {
                float rad = 10 * s + i * 14 * s;
                byte alpha = (byte)((0.03f + i * 0.015f) * 255);
                var wavePaint = new SKPaint
                {
                    Color = new SKColor(0x7C, 0x3A, 0xED, alpha),
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = (1.5f - i * 0.1f) * s
                };
                if (i % 2 == 0)
                    wavePaint.PathEffect = SKPathEffect.CreateDash(new[] { 4 * s, 3 * s }, 0);
                canvas.DrawCircle(cx, waveCy, rad, wavePaint);
            }
            y += 80 * s;

            // ============================================================
            // GRADIENT SEPARATOR
            // ============================================================
            canvas.DrawLine(ml, y, mr, y, new SKPaint { Shader = sepGrad, StrokeWidth = 1 * s, IsAntialias = true });
            y += 22 * s;

            // ============================================================
            // FOOTER
            // ============================================================
            var brandText = "EventPro by Eddy Graphix";
            var brandW = brandFont.MeasureText(brandText, new SKPaint());
            canvas.DrawText(brandText, cx - brandW / 2f, y + 16 * s, brandFont, new SKPaint { Color = w50, IsAntialias = true });

            titleFont.Dispose();
            nameFont.Dispose();

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return new MemoryStream(data.ToArray());
        }

        // ============================================================
        // DECORATIVE HELPERS
        // ============================================================
        private static void DrawDotGrid(SKCanvas c, int w, int h, SKColor bg, float s)
        {
            var dot = new SKPaint
            {
                Color = new SKColor(0xFF, 0xFF, 0xFF, 0x04),
                IsAntialias = true
            };
            float spacing = 40 * s;
            for (float x = 0; x < w; x += spacing)
                for (float y = 0; y < h; y += spacing)
                    c.DrawCircle(x, y, 1.2f * s, dot);
        }

        private static void DrawCornerOrnament(SKCanvas c, float x, float y, float size, SKColor color)
        {
            var p = new SKPaint { Color = new SKColor(color.Red, color.Green, color.Blue, 0x30), IsAntialias = true, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke };
            float s2 = size / 2f;
            c.DrawLine(x, y + s2, x + s2, y + s2, p);
            c.DrawLine(x + s2, y, x + s2, y + s2, p);
        }

        private static async Task<byte[]?> GetPhotoBytesAsync(Attendee attendee)
        {
            if (string.IsNullOrEmpty(attendee.PhotoUrl))
                return null;

            var cacheDir = Path.Combine(FileSystem.CacheDirectory, "photos");
            Directory.CreateDirectory(cacheDir);
            var cachePath = Path.Combine(cacheDir, $"attendee_{attendee.Id}.jpg");

            if (File.Exists(cachePath))
                return await File.ReadAllBytesAsync(cachePath);

            try
            {
                var bytes = await _photoClient.GetByteArrayAsync(attendee.PhotoUrl);
                await File.WriteAllBytesAsync(cachePath, bytes);
                return bytes;
            }
            catch
            {
                return null;
            }
        }

        public async Task<string> SaveTicketImageAsync(Attendee attendee)
        {
            var stream = await GenerateTicketImageAsync(attendee);
            var fileName = $"ticket_{attendee.FullName}.png";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            using var fileStream = File.OpenWrite(filePath);
            await stream.CopyToAsync(fileStream);

            return filePath;
        }
    }
}
