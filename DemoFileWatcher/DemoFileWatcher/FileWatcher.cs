using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace DemoFileWatcher
{
    public class FileWatcher
    {
        private FileSystemWatcher _fileWatcher;
        public FileWatcher()
        {
            _fileWatcher = new FileSystemWatcher(GetLocation());
            _fileWatcher.Created += _fileWatcher_Created;
            _fileWatcher.Changed += _fileWatcher_Changed;
            _fileWatcher.Deleted += _fileWatcher_Deleted;
            _fileWatcher.EnableRaisingEvents = true;
        }

        private string GetLocation()
        {
            string value = string.Empty;
            try
            {
                value = System.Configuration.ConfigurationManager.AppSettings["location"];
            }
            catch(Exception ex)
            {
                //
            }
            return value;
        }

        private void _fileWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("file deleted");
        }

        private void _fileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("file changed");
        }

        private void _fileWatcher_Created(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("file added");
            SAS();
        }

        private void SAS()
        {
            //Parse the connection string and return a reference to the storage account.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"]);
            //Create the blob client object.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            //Get a reference to a container to use for the sample code, and create it if it does not exist.
            CloudBlobContainer container = blobClient.GetContainerReference("documentparserdemo");
            BlobContainerPermissions perms = container.GetPermissions();
            perms.SharedAccessPolicies.Clear();
            container.SetPermissions(perms);
            string sharedAccessPolicyName = "demopolicy";
            CreateSharedAccessPolicy(blobClient, container, sharedAccessPolicyName);
            string Uri = GetBlobSasUriWithPolicy(container, "demopolicy");
            Console.WriteLine("Blob SAS URI: " + Uri);
            UseBlobSAS(Uri);
            Console.WriteLine();
        }

        private string GetContainerSasUri(CloudBlobContainer container)
        {
            //Set the expiry time and permissions for the container.
            //In this case no start time is specified, so the shared access signature becomes valid immediately.
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy();
            sasConstraints.SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddHours(24);
            sasConstraints.Permissions = SharedAccessBlobPermissions.List | SharedAccessBlobPermissions.Write;

            //Generate the shared access signature on the container, setting the constraints directly on the signature.
            string sasContainerToken = container.GetSharedAccessSignature(sasConstraints);

            //Return the URI string for the container, including the SAS token.
            return container.Uri + sasContainerToken;
        }

        private string GetBlobSasUri(CloudBlobContainer container)
        {
            //Get a reference to a blob within the container.
            CloudBlockBlob blob = container.GetBlockBlobReference("sasblob.txt");

            //Upload text to the blob. If the blob does not yet exist, it will be created.
            //If the blob does exist, its existing content will be overwritten.
            string blobContent = "This blob will be accessible to clients via a shared access signature (SAS).";
            blob.UploadText(blobContent);

            //Set the expiry time and permissions for the blob.
            //In this case, the start time is specified as a few minutes in the past, to mitigate clock skew.
            //The shared access signature will be valid immediately.
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy();
            sasConstraints.SharedAccessStartTime = DateTimeOffset.UtcNow.AddMinutes(-5);
            sasConstraints.SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddHours(24);
            sasConstraints.Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Write;

            //Generate the shared access signature on the blob, setting the constraints directly on the signature.
            string sasBlobToken = blob.GetSharedAccessSignature(sasConstraints);

            //Return the URI string for the container, including the SAS token.
            return blob.Uri + sasBlobToken;
        }

        static void CreateSharedAccessPolicy(CloudBlobClient blobClient, CloudBlobContainer container,string policyName)
        {
            //Get the container's existing permissions.
            BlobContainerPermissions permissions = container.GetPermissions();

            //Create a new shared access policy and define its constraints.
            SharedAccessBlobPolicy sharedPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddHours(24),
                Permissions = SharedAccessBlobPermissions.Write | SharedAccessBlobPermissions.List | SharedAccessBlobPermissions.Read
            };

            //Add the new policy to the container's permissions, and set the container's permissions.
            permissions.SharedAccessPolicies.Add(policyName, sharedPolicy);
            container.SetPermissions(permissions);
        }

        private string GetBlobSasUriWithPolicy(CloudBlobContainer container, string policyName)
        {
            //Get a reference to a blob within the container.
            CloudBlockBlob blob = container.GetBlockBlobReference("sasblobpolicy.txt");

            //Upload text to the blob. If the blob does not yet exist, it will be created.
            //If the blob does exist, its existing content will be overwritten.
            string blobContent = "This blob will be accessible to clients via a shared access signature. " +
            "A stored access policy defines the constraints for the signature.";
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(blobContent));
            ms.Position = 0;
            using (ms)
            {
                blob.UploadFromStream(ms);
            }

            //Generate the shared access signature on the blob.
            string sasBlobToken = blob.GetSharedAccessSignature(null, policyName);

            //Return the URI string for the container, including the SAS token.
            return blob.Uri + sasBlobToken;
        }


        static void UseBlobSAS(string sas)
        {
            //Try performing blob operations using the SAS provided.

            //Return a reference to the blob using the SAS URI.
            CloudBlockBlob blob = new CloudBlockBlob(new Uri(sas));

            //Write operation: Write a new blob to the container.
            try
            {
                string blobContent = "This blob was created with a shared access signature granting write permissions to the blob. ";
                MemoryStream msWrite = new MemoryStream(Encoding.UTF8.GetBytes(blobContent));
                msWrite.Position = 0;
                using (msWrite)
                {
                    blob.UploadFromStream(msWrite);
                }
                Console.WriteLine("Write operation succeeded for SAS " + sas);
                Console.WriteLine();
            }
            catch (StorageException e)
            {
                Console.WriteLine("Write operation failed for SAS " + sas);
                Console.WriteLine("Additional error information: " + e.Message);
                Console.WriteLine();
            }

            //Read operation: Read the contents of the blob.
            try
            {
                MemoryStream msRead = new MemoryStream();
                using (msRead)
                {
                    blob.DownloadToStream(msRead);
                    msRead.Position = 0;
                    using (StreamReader reader = new StreamReader(msRead, true))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            Console.WriteLine(line);
                        }
                    }
                }
                Console.WriteLine("Read operation succeeded for SAS " + sas);
                Console.WriteLine();
            }
            catch (StorageException e)
            {
                Console.WriteLine("Read operation failed for SAS " + sas);
                Console.WriteLine("Additional error information: " + e.Message);
                Console.WriteLine();
            }

            //Delete operation: Delete the blob.
            try
            {
                blob.Delete();
                Console.WriteLine("Delete operation succeeded for SAS " + sas);
                Console.WriteLine();
            }
            catch (StorageException e)
            {
                Console.WriteLine("Delete operation failed for SAS " + sas);
                Console.WriteLine("Additional error information: " + e.Message);
                Console.WriteLine();
            }
        }


    }
}
