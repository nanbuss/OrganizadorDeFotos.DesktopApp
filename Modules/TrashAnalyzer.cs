using System.IO;
using SixLabors.ImageSharp;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using System.Threading;
using System.Threading.Tasks;

namespace OrganizadorDeFotos.DesktopApp.Modules
{
    internal static class TrashAnalyzer
    {
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff" };

        private static readonly OcrEngine? _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
        private static readonly SemaphoreSlim _ocrSemaphore = new SemaphoreSlim(1, 1);

        public static async Task<List<TrashCandidate>> FindTrashCandidatesAsync(string folderPath)
        {
            var candidates = new System.Collections.Concurrent.ConcurrentBag<TrashCandidate>();
            var imageFiles = Directory.GetFiles(folderPath)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            await Parallel.ForEachAsync(imageFiles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (filePath, ct) =>
            {
                var candidate = await AnalyzeItemAsync(filePath);
                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            });

            return candidates.ToList();
        }

        private static async Task<TrashCandidate?> AnalyzeItemAsync(string filePath)
        {
            string fileName = Path.GetFileName(filePath).ToLowerInvariant();
            if (fileName.Contains("screenshot") || fileName.Contains("captura"))
            {
                return new TrashCandidate(ImageItem.FromFile(filePath))
                {
                    Reason = "Captura",
                    Confidence = 0.95
                };
            }

            try
            {
                var info = Image.Identify(filePath);
                if (info != null && IsStandardMobileAspectRatio(info.Width, info.Height))
                {
                    return new TrashCandidate(ImageItem.FromFile(filePath))
                    {
                        Reason = "Captura",
                        Confidence = 0.85
                    };
                }
            }
            catch
            {
                return null;
            }

            // Detección de texto avanzado usando Windows OCR
            bool isTextDocument = await IsTextDocumentAsync(filePath);
            if (isTextDocument)
            {
                return new TrashCandidate(ImageItem.FromFile(filePath))
                {
                    Reason = "Comprobante / Captura de texto",
                    Confidence = 0.90
                };
            }

            var histogram = await AnalyzeBrightnessAsync(filePath);
            if (histogram.IsDark)
            {
                return new TrashCandidate(ImageItem.FromFile(filePath))
                {
                    Reason = "Foto muy oscura",
                    Confidence = histogram.Confidence
                };
            }

            if (histogram.IsBright)
            {
                return new TrashCandidate(ImageItem.FromFile(filePath))
                {
                    Reason = "Foto muy clara",
                    Confidence = histogram.Confidence
                };
            }

            return null;
        }

        private static async Task<(bool IsDark, bool IsBright, double Confidence)> AnalyzeBrightnessAsync(string filePath)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(filePath));
                using var stream = await file.OpenAsync(FileAccessMode.Read);
                var decoder = await BitmapDecoder.CreateAsync(stream);

                // Reducir la imagen 10x disminuye exponencialmente el tamaño en memoria y los iteradores
                var transform = new BitmapTransform
                {
                    ScaledWidth = (uint)Math.Max(1, decoder.PixelWidth / 10),
                    ScaledHeight = (uint)Math.Max(1, decoder.PixelHeight / 10)
                };

                var pixelDataInfo = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Ignore,
                    transform,
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);

                byte[] pixels = pixelDataInfo.DetachPixelData();

                int blackCount = 0;
                int whiteCount = 0;
                int totalCount = pixels.Length / 4;
                double brightnessSum = 0;

                for (int i = 0; i < pixels.Length; i += 4)
                {
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];

                    double luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0;
                    brightnessSum += luminance;

                    if (luminance <= 0.05)
                        blackCount++;
                    else if (luminance >= 0.95)
                        whiteCount++;
                }

                if (totalCount == 0)
                {
                    return (false, false, 0.0);
                }

                double blackRatio = blackCount / (double)totalCount;
                double whiteRatio = whiteCount / (double)totalCount;
                double average = brightnessSum / totalCount;

                if (blackRatio > 0.92)
                {
                    return (true, false, Math.Min(1.0, 0.7 + blackRatio * 0.3));
                }

                if (whiteRatio > 0.92)
                {
                    return (false, true, Math.Min(1.0, 0.7 + whiteRatio * 0.3));
                }

                return (false, false, average);
            }
            catch
            {
                return (false, false, 0.0);
            }
        }

        private static bool IsStandardMobileAspectRatio(int width, int height)
        {
            if (width == 0 || height == 0) return false;

            var standardRatios = new (int width, int height)[]
            {
                (1920, 1080),
                (1080, 1920),
                (2340, 1080),
                (1080, 2340),
                (2400, 1080),
                (1080, 2400),
                (1170, 2532),
                (1080, 2400),
                (1125, 2436),
                (720, 1280),
                (1080, 2280),
                (1440, 3040)
            };

            foreach (var ratio in standardRatios)
            {
                if ((long)width * ratio.height == (long)height * ratio.width)
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task<bool> IsTextDocumentAsync(string filePath)
        {
            if (_ocrEngine == null) return false;

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(filePath));
                using var stream = await file.OpenAsync(FileAccessMode.Read);
                var decoder = await BitmapDecoder.CreateAsync(stream);
                using var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                OcrResult result;
                await _ocrSemaphore.WaitAsync();
                try
                {
                    result = await _ocrEngine.RecognizeAsync(softwareBitmap);
                }
                finally
                {
                    _ocrSemaphore.Release();
                }

                if (result == null || result.Lines.Count == 0) return false;

                // 1. Sensibilidad aumentada: más de 10 letras o dígitos
                if (result.Text.Count(c => char.IsLetterOrDigit(c)) > 10) return true;

                // 2. Alta cantidad de texto plano (incluso recortado)
                if (result.Lines.Count >= 12) return true;

                // 3. Palabras clave específicas (Finanzas, Redes Sociales, Publicidad)
                string fullText = result.Text.ToLowerInvariant();
                string[] keywords = {
                    // Finanzas
                    "transferencia", "comprobante", "cbu", "cvu", "alias","cuenta",
                    "importe", "saldo", "pago exitoso", "mercado pago", "mercadopago",
                    "banco", "factura", "ticket", "aprobado", "santander", "galicia", "bbva",
                    // Redes / Chat
                    "whatsapp", "instagram", "responder", "reenviado", "escribe un mensaje",
                    "mensaje", "comentar", "compartir", "me gusta", "seguidores",
                    // Publicidad / E-mails
                    "promoción", "oferta", "descuento", "envío gratis", "compra ahora",
                    "suscripción", "cancelar", "ver online", "click aquí", ".com", "thanks",
                    "gracias", "pedidos"
                };

                if (keywords.Any(k => fullText.Contains(k))) return true;

                // 4. Sensibilidad Espacial (Densidad de Texto)
                // Esto es clave: calcula el área geométrica que ocupan las letras detectadas
                // respecto al lienzo total. Un meme, chat o recorte de email usualmente 
                // tiene una altísima densidad de letras en relación al espacio.
                double totalTextArea = 0;
                foreach (var line in result.Lines)
                {
                    foreach (var word in line.Words)
                    {
                        totalTextArea += (word.BoundingRect.Width * word.BoundingRect.Height);
                    }
                }

                double imageArea = softwareBitmap.PixelWidth * softwareBitmap.PixelHeight;
                double textDensity = totalTextArea / imageArea;

                // Si más del 3.5% de los PÍXELES de toda la imagen están compuestos por letras perfectas
                // reconocidas por la IA nativa del sistema, es casi seguro un texto, captura o meme.
                // Una foto normal de personas o paisajes rara vez alcanza más del 0.5% de "área" de texto confuso.
                if (textDensity > 0.035) return true;

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
