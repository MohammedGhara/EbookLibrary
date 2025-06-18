using System.Diagnostics;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;

namespace OnlineLibrary.Controllers;

[Route("EbookConversion")]
public class EbookConversionController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly string _calibrePath; // Path to Calibre's ebook-convert tool

    public EbookConversionController(IConfiguration configuration)
    {
        _configuration = configuration;
        _calibrePath = _configuration["CalibreSettings:CalibrePath"];
    }

    [HttpGet("Convert")]
    public async Task<IActionResult> ConvertBook(string bookId, string format = "pdf")
    {
        // 1) Fetch the local or remote PDF path from the database
        var pdfPath = GetBookPdfPath(bookId);
        if (string.IsNullOrEmpty(pdfPath))
        {
            return NotFound("Book PDF path not found in the database.");
        }

        // If it's a remote URL, handle it accordingly
        bool isRemoteFile = pdfPath.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        string tempPdfPath = null;

        if (isRemoteFile)
        {
            // Download the remote file to a temporary path
            var tempFolder = Path.GetTempPath();
            tempPdfPath = Path.Combine(tempFolder, Path.GetRandomFileName() + ".pdf");

            try
            {
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync(pdfPath);
                    if (!response.IsSuccessStatusCode)
                    {
                        return NotFound($"Remote file not found or inaccessible at {pdfPath}.");
                    }

                    var pdfBytes = await response.Content.ReadAsByteArrayAsync();
                    await System.IO.File.WriteAllBytesAsync(tempPdfPath, pdfBytes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading PDF: {ex.Message}");
                return StatusCode(500, "Error occurred while downloading the PDF.");
            }
        }
        else
        {
            // Ensure the local file exists
            if (!System.IO.File.Exists(pdfPath))
            {
                return NotFound("PDF file does not exist on the server.");
            }

            tempPdfPath = pdfPath; // Use the local path directly
        }

        // 2) Return the local or downloaded file directly if the user requests a PDF
        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
        {
            var fileBytes = await System.IO.File.ReadAllBytesAsync(tempPdfPath);
            return File(fileBytes, "application/pdf", Path.GetFileName(tempPdfPath));
        }

        // Supported formats
        var validFormats = new HashSet<string> { "epub", "mobi", "f2b" };
        if (!validFormats.Contains(format.ToLower()))
        {
            return BadRequest("Only pdf, epub, mobi, or f2b formats are supported.");
        }

        // 3) Prepare paths for conversion
        var tempFolderPath = Path.GetTempPath();
        var convertedFileBase = Path.GetRandomFileName();
        var calibreFormat = format.Equals("f2b", StringComparison.OrdinalIgnoreCase) ? "epub" : format;
        var realCalibreOutput = Path.Combine(tempFolderPath, $"{convertedFileBase}.{calibreFormat}");
        var finalOutputExtension = format.Equals("f2b", StringComparison.OrdinalIgnoreCase) ? "f2b" : format;
        var finalFileName = Path.Combine(tempFolderPath, $"{convertedFileBase}.{finalOutputExtension}");
        var mimeType = GetMimeType(format);

        try
        {
            // 4) Run the Calibre conversion tool
            var startInfo = new ProcessStartInfo
            {
                FileName = _calibrePath,
                Arguments = $"\"{tempPdfPath}\" \"{realCalibreOutput}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                await process.StandardOutput.ReadToEndAsync();
                await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    return StatusCode(500, "Conversion process failed.");
                }
            }

            if (!System.IO.File.Exists(realCalibreOutput))
            {
                return NotFound("Converted file not found after conversion.");
            }

            if (format.Equals("f2b", StringComparison.OrdinalIgnoreCase))
            {
                System.IO.File.Move(realCalibreOutput, finalFileName); // Rename if needed
            }
            else
            {
                finalFileName = realCalibreOutput; // No rename required
            }

            // 5) Read and return the converted file
            var fileBytes = await System.IO.File.ReadAllBytesAsync(finalFileName);
            return File(fileBytes, mimeType, $"{Path.GetFileNameWithoutExtension(tempPdfPath)}.{format.ToLower()}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during conversion: {ex.Message}");
            return StatusCode(500, "Error occurred while converting the file.");
        }
        finally
        {
            // Cleanup temporary files
            CleanupTempFiles(new[] { tempPdfPath, realCalibreOutput, finalFileName });
        }
    }

    /// <summary>
    /// Fetch the local or remote PDF path from the database based on BookID.
    /// </summary>
    private string GetBookPdfPath(string bookId)
    {
        string pdfPath = null;
        string connString = _configuration.GetConnectionString("DefaultConnection");

        using (SqlConnection conn = new SqlConnection(connString))
        {
            conn.Open();
            string sql = "SELECT BookUrl FROM Books WHERE BookID = @BookID";
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@BookID", bookId);
                object result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    pdfPath = result.ToString();
                }
            }
        }

        return pdfPath;
    }

    private string GetMimeType(string format)
    {
        return format.ToLower() switch
        {
            "pdf" => "application/pdf",
            "epub" => "application/epub+zip",
            "mobi" => "application/x-mobipocket-ebook",
            "f2b" => "application/octet-stream",
            _ => "application/octet-stream"
        };
    }

    private void CleanupTempFiles(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            try
            {
                if (System.IO.File.Exists(file))
                {
                    System.IO.File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up file {file}: {ex.Message}");
            }
        }
    }
}
