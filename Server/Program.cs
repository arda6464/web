using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Web sitesinin bulunduğu ana dizin
            string webRoot = @"c:\Project\web";

            int port = 9339;
            string url = $"http://localhost:{port}/";

            using var listener = new HttpListener();
            listener.Prefixes.Add(url);
            
            try
            {
                listener.Start();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[SUNUCU BAŞLADI] Dinlenen adres: {url}");
                Console.WriteLine($"[WEB DİZİNİ] {webRoot}");
                Console.WriteLine("Durdurmak için CTRL+C tuşlarına basın...\n");
                Console.ResetColor();

                while (true)
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context, webRoot)); // İstekleri paralel işle
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[HATA] {ex.Message}");
                Console.ResetColor();
            }
        }

        static async Task HandleRequest(HttpListenerContext context, string webRoot)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            try
            {
                // İstenen dosya yolunu belirle
                string rawUrl = request.RawUrl ?? "/";
                string urlPath = rawUrl.Split('?')[0]; // Query parametrelerini ayır
                
                if (urlPath == "/")
                    urlPath = "/index.html";

                // URL encode edilmiş boşlukları vs. düzelt
                urlPath = Uri.UnescapeDataString(urlPath);
                
                // Tam dosya yolu
                string filePath = Path.Combine(webRoot, urlPath.TrimStart('/'));

                // Log: İstek geldi
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] İSTEK: {request.HttpMethod} {urlPath}");

                // Güvenlik: Directory traversal saldırılarını önle (örn: /../../windows/system32)
                if (!Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(webRoot), StringComparison.OrdinalIgnoreCase))
                {
                    SendError(response, 403, "Forbidden");
                    return;
                }

                if (File.Exists(filePath))
                {
                    byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                    
                    response.ContentType = GetMimeType(filePath);
                    response.ContentLength64 = fileBytes.Length;
                    response.StatusCode = 200;

                    await response.OutputStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                    
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] YANIT: 200 OK - {urlPath}");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] YANIT: 404 BULUNAMADI - {urlPath}");
                    Console.ResetColor();
                    SendError(response, 404, "Not Found");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SUNUCU HATASI: {ex.Message}");
                Console.ResetColor();
                SendError(response, 500, "Internal Server Error");
            }
            finally
            {
                response.Close();
            }
        }

        static void SendError(HttpListenerResponse response, int statusCode, string message)
        {
            try
            {
                response.StatusCode = statusCode;
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes($"<html><body><h1>{statusCode} - {message}</h1></body></html>");
                response.ContentLength64 = bytes.Length;
                response.ContentType = "text/html";
                response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch { /* Hata gönderilirken oluşan hataları yoksay */ }
        }

        static string GetMimeType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".mp4" => "video/mp4",
                ".mp3" => "audio/mpeg",
                _ => "application/octet-stream"
            };
        }
    }
}
