using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Diagnostics;
using System.Linq;

namespace BlenderConverter
{
    public static class ConvertFunction
    {
        [FunctionName("ConvertFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            [Blob("input-container-name", FileAccess.Read)] CloudBlobContainer inputContainer,
            [Blob("output-container-name/{name}.png", FileAccess.Write)] CloudBlobContainer outputContainer,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            // Get the list of blobs in the input container
            BlobContinuationToken continuationToken = null;
            var inputBlobs = new List<CloudBlockBlob>();
            do
            {
                var response = await inputContainer.ListBlobsSegmentedAsync(null, continuationToken);
                continuationToken = response.ContinuationToken;
                inputBlobs.AddRange(response.Results.OfType<CloudBlockBlob>());
            }
            while (continuationToken != null);

            // Convert each input blob to a PNG file
            foreach (var inputBlob in inputBlobs)
            {
                log.LogInformation($"Converting {inputBlob.Name} to PNG...");

                // Check if the output blob already exists
                var outputBlob = outputContainer.GetBlockBlobReference($"{Path.GetFileNameWithoutExtension(inputBlob.Name)}.png");
                if (await outputBlob.ExistsAsync())
                {
                    log.LogInformation($"Output file already exists for {inputBlob.Name}");
                    continue;
                }

                // Read the input file content from the Blob Storage container
                byte[] inputFileContent;
                using (var memoryStream = new MemoryStream())
                {
                    await inputBlob.DownloadToStreamAsync(memoryStream);
                    inputFileContent = memoryStream.ToArray();
                }

                // Create a temporary copy of the input file
                using (var tempInputFile = new TemporaryFile(inputBlob.Name, inputFileContent))
                {
                    // Use Blender to convert the input file to a PNG file
                    string tempOutputFilePath = Path.GetTempFileName();
                    RunBlenderScript(tempInputFile.FilePath, tempOutputFilePath);
                    byte[] pngFileContent = File.ReadAllBytes(tempOutputFilePath);
                    File.Delete(tempOutputFilePath);

                    // Save the PNG file to the output Blob Storage container
                    using (var memoryStream = new MemoryStream(pngFileContent))
                    {
                        await outputBlob.UploadFromStreamAsync(memoryStream);
                    }
                }

                log.LogInformation($"Converted {inputBlob.Name} to PNG.");
            }

            return new OkObjectResult($"Converted {inputBlobs.Count} files to PNG.");
        }

        private static void RunBlenderScript(string inputFilePath, string outputFilePath)
{
    string blenderExePath = @"C:\Program Files\Blender Foundation\Blender 3.4\blender.exe";
    string scriptPath = @"C:\Users\ME\__mesh2png.py";
    string args = $"-b \"{inputFilePath}\" -P \"{scriptPath}\" -- \"{inputFilePath}\" \"{outputFilePath}\" 43.526";
    ProcessStartInfo startInfo = new ProcessStartInfo(blenderExePath, args);
    startInfo.UseShellExecute = false;
    startInfo.CreateNoWindow = true;
    startInfo.RedirectStandardOutput = true;
    using (Process process = new Process())
    {
        process.StartInfo = startInfo;
        process.Start();
        process.WaitForExit();
    }
}
