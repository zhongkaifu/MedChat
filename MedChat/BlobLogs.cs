using System;
using System.IO;
using System.Threading.Tasks;
using AdvUtils;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace MedChat
{
    public class BlobLogs
    {
        BlobContainerClient containerClient = null;
        AppendBlobClient? blobClient = null;
        string logFileName;

        static private object locker = new object();

        public BlobLogs(string connectionString, string containerName)
        {
            if (String.IsNullOrEmpty(connectionString) || String.IsNullOrEmpty(containerName))
            {
                return;
            }

            lock (locker)
            {
                // Create a BlobServiceClient using the connection string
                BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
                // Get a reference to a container
                containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            }
        }

        public static string GetTimeStamp(DateTime timeStamp)
        {
            return string.Format("{0:yyyy}_{0:MM}_{0:dd}", timeStamp);
        }

        public void WriteLine(string content)
        {
            try
            {
                if (containerClient == null)
                {
                    return;
                }

                string logBlobName = GetTimeStamp(DateTime.Now) + ".txt";
                if (blobClient == null || logBlobName != logFileName)
                {
                    blobClient = containerClient.GetAppendBlobClient(logBlobName);
                    blobClient.CreateIfNotExists();
                    logFileName = logBlobName;
                }

                lock (locker)
                {
                    using (MemoryStream stream = new MemoryStream())
                    {
                        StreamWriter writer = new StreamWriter(stream);
                        writer.WriteLine(content);
                        writer.Flush();
                        stream.Position = 0;

                        blobClient.AppendBlock(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Failed to add log to blob storage. Error = '{ex.Message}' Call Stack = '{ex.StackTrace}'");
            }
        }
    }
}
