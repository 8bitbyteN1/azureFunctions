using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;

public static class BlenderConverter
{
    public static void ConvertDirectory(string inputContainerName, string outputContainerName, string inputConnectionString, string outputConnectionString)
    {
        // Path to the Blender executable on the local machine
        var blenderExePath = @"C:\Program Files\Blender Foundation\Blender 3.4\blender.exe";

        // Path to the Python script to run in Blender
        var pythonScriptPath = @"C:\Users\ME\__mesh2png.py";

        // Parse the connection strings for the input and output Blob Storage accounts
        var inputAccount = CloudStorageAccount.Parse(inputConnectionString);
        var inputClient = inputAccount.CreateCloudBlobClient();

        var outputAccount = CloudStorageAccount.Parse(outputConnectionString);
        var outputClient = outputAccount.CreateCloudBlobClient();

        // Get references to the input and output Blob Storage containers
        var inputContainer = inputClient.GetContainerReference(inputContainerName);
        var outputContainer = outputClient.GetContainerReference(outputContainerName);

        // Loop through all blobs in the input container
        foreach (var inputBlob in inputContainer.ListBlobs())
        {
            // Only process blobs of type CloudBlockBlob
            if (inputBlob.GetType() == typeof(CloudBlockBlob))
            {
                // Get the name of the input blob
                var inputBlobName = ((CloudBlockBlob)inputBlob).Name;

                // Open an input stream to read the contents of the input blob
                var inputFileStream = ((CloudBlockBlob)inputBlob).OpenRead();

                // Construct the name of the output blob by changing the file extension to .png
                var outputBlobName = Path.GetFileNameWithoutExtension(inputBlobName) + ".png";

                // Get a reference to the output blob
                var outputBlob = outputContainer.GetBlockBlobReference(outputBlobName);

                // Open an output stream to write the contents of the output blob
                var outputFileStream = outputBlob.OpenWrite();

                // Construct the command line arguments to pass to Blender
                var processInfo = new ProcessStartInfo
                {
                    FileName = blenderExePath,
                    Arguments = $"-b \"{inputBlobName}\" -P \"{pythonScriptPath}\" -- \"{inputBlobName}\" \"{outputBlobName}\" 43.526",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Launch a new process to execute the command line
                using (var process = Process.Start(processInfo))
                {
                    // Wait for the process to exit
                    process.WaitForExit();

                    // Check the exit code of the process to see if the conversion was successful
                    if (process.ExitCode != 0)
                    {
                        Console.Error.WriteLine($"Error converting blob {inputBlobName}");
                    }
                    else
                    {
                        Console.WriteLine($"Blob {inputBlobName} converted to {outputBlobName}");
                    }
                }

                // Copy the contents of the input stream to the output stream to save the converted PNG file to Blob Storage
                inputFileStream.CopyTo(outputFileStream);

                // Close the input and output streams
                inputFileStream.Close();
                outputFileStream.Close();
            }
        }
    }
}
