using FikaAmazonAPI;
using FikaAmazonAPI.Utils;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static FikaAmazonAPI.Utils.Constants;

namespace Amazon_Document_Transport_Utility
{
    internal class Program
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        static void Main(string[] args)
        {
            // Path to the config application exe. We need this because the application is usually ran from task scheduler.
            string path = Path.GetDirectoryName(AppContext.BaseDirectory);

            // Load the config settings
            var config = new Config();
            try
            {
                config = JsonSerializer.Deserialize<Config>(File.ReadAllText(Path.Combine(path, "Config.json")));
            }
            catch (Exception e)
            {
                Console.WriteLine("Error loading config file: " + e);
                logger.Debug("Error loading config file: " + e);
                return;
            }

            // Setup the SP-API connection configuration class.
            AmazonConnection amazonConnection = new AmazonConnection(new AmazonCredential()
            {
                AccessKey = config.accessKey,
                SecretKey = config.secretKey,
                RoleArn = config.roleARN,
                ClientId = config.clientId,
                ClientSecret = config.clientSecret,
                RefreshToken = config.refreshToken,
                MarketPlace =  MarketPlace.US //MarketPlace.GetMarketplaceByCountryCode(config.marketplace),
            });

            // Check if there is a download document type specified.
            if (!String.IsNullOrEmpty(config.downloadDocumentType))
            {
                var successful = DownloadDocumentSwitcher(amazonConnection, config);
                logger.Info("Downloading document: " + config.downloadDocumentType + " Result: " + successful);
            }

            // Check if there is a upload document type specified.
            // If there is we scan the specified folder and upload ALL documents in that folder.
            if (!String.IsNullOrEmpty(config.uploadDocumentType))
            {
                logger.Info("Scanning upload documents folder.");
                var successful = UploadDocumentSwitcher(amazonConnection, config);
                logger.Info("Uploading documents: " + config.uploadDocumentType + " Result: " + successful);
            }

            // Explicitly close the logger so it clears the buffers. If you leave it to chance sometimes it will not purge the buffer and close the logs.
            LogManager.Shutdown();
        }

        /// <summary>
        /// There is support for a lot of different document types to download. We have to send specific types to their respective functions to download.
        /// </summary>
        /// <param name="amazonConnection"></param>
        /// <param name="downloadDocumteType"></param>
        /// <param name="documentDownloadFolder"></param>
        /// <param name="downloadDocumentFileName"></param>
        /// <returns></returns>
        private static string DownloadDocumentSwitcher(AmazonConnection amazonConnection, Config config)
        {
            switch (config.downloadDocumentType)
            {
                case "GET_FLAT_FILE_ACTIONABLE_ORDER_DATA_SHIPPING":
                case "GET_FLAT_FILE_ALL_ORDERS_DATA_BY_ORDER_DATE_GENERAL":
                case "GET_FLAT_FILE_ORDER_REPORT_DATA_SHIPPING":
                    return DownloadFlatFileOrderReport(amazonConnection, config);

                default:
                    return "Failed: Invalid document type.";
            }
        }

        /// <summary>
        /// There is support for a lot of different document types to upload. We have to send specific types to their respective functions to upload.
        /// </summary>
        /// <param name="amazonConnection"></param>
        /// <param name="uploadDocumentType"></param>
        /// <param name="documentUploadFolder"></param>
        /// <param name="documentUploadCompletedFolder"></param>
        /// <param name="documentUploadFailedFolder"></param>
        /// <returns></returns>
        private static string UploadDocumentSwitcher(AmazonConnection amazonConnection, Config config)
        {
            switch (config.uploadDocumentType)
            {
                case "POST_FLAT_FILE_PRICEANDQUANTITYONLY_UPDATE_DATA":
                case "POST_FLAT_FILE_FULFILLMENT_DATA":
                    return UploadFlatFileFeed(amazonConnection, config);
                default:
                    return "Failed: Invalid document type.";
            }
        }

        /// <summary>
        /// Upload flat file tab deliminated Amazon feed files. Scans folder and uploads all files from that folder.
        /// Moves file to completed or failed folder when done.
        /// </summary>
        /// <param name="amazonConnection"></param>
        /// <param name="uploadDocumentType"></param>
        /// <param name="documentUploadFolder"></param>
        /// <param name="documentUploadCompletedFolder"></param>
        /// <param name="documentUploadFailedFolder"></param>
        /// <returns></returns>
        private static string UploadFlatFileFeed(AmazonConnection amazonConnection, Config config)
        {
            // Scan the upload document folder for files.
            if (Directory.Exists(config.documentUploadFolder))
            {
                try
                {
                    string[] files = Directory.GetFiles(config.documentUploadFolder);

                    // Convert the string report type to the enum.
                    FeedType reportType;
                    Enum.TryParse<FeedType>(config.uploadDocumentType, out reportType);

                    foreach (var file in files)
                    {
                        var feedID = amazonConnection.Feed.SubmitFeed(file, reportType, null, null, ContentType.TXT);

                        Thread.Sleep(1000 * 30);
                        var feedOutput = amazonConnection.Feed.GetFeed(feedID);

                        while (feedOutput.ProcessingStatus == FikaAmazonAPI.AmazonSpApiSDK.Models.Feeds.Feed.ProcessingStatusEnum.INPROGRESS)
                        {
                            Console.WriteLine("Monitoring status of uploaded feed id: " + feedID + " Status: " + feedOutput.ProcessingStatus);
                            Thread.Sleep(1000 * 30);
                            feedOutput = amazonConnection.Feed.GetFeed(feedID);
                        }

                        Console.WriteLine("Uploading file: " + Path.GetFileName(file) + " Results: " + feedOutput.ProcessingStatus + " Feed ID: " + feedID);
                        logger.Info("Uploading file: " + Path.GetFileName(file) + " Results: " + feedOutput.ProcessingStatus + " Feed ID: " + feedID);

                        if (feedOutput.ProcessingStatus == FikaAmazonAPI.AmazonSpApiSDK.Models.Feeds.Feed.ProcessingStatusEnum.DONE)
                        {
                            File.Move(file, Path.Combine(config.documentUploadCompletedFolder, Path.GetFileName(file)));
                        }
                        else
                        {
                            File.Move(file, Path.Combine(config.documentUploadFailedFolder, Path.GetFileName(file)));
                            logger.Debug("Failed to upload flat file feed: " + config.uploadDocumentType + " " + file);
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Debug("Error uploading flat file feed: " + config.uploadDocumentType + " " + e.ToString());
                    return "Failed";
                }
            }
            else
            {
                logger.Debug("Failed: Upload documents folder does not exist: " + config.documentUploadFolder);
                return "Failed: Upload documents folder does not exist: " + config.documentUploadFolder;
            }

            return "Success";
        }

        /// <summary>
        /// Function to download flat file report types. 
        /// </summary>
        /// <param name="amazonConnection"></param>
        /// <param name="downloadDocumteType"></param>
        /// <param name="documentDownloadFolder"></param>
        /// <param name="downloadDocumentFileName"></param>
        /// <returns></returns>
        private static string DownloadFlatFileOrderReport(AmazonConnection amazonConnection, Config config)
        {

            // Allow start and end dates. In the config file we have positive numbers but convert them to negatives to go backwards.
            DateTime startDate = DateTime.UtcNow.AddDays(-30);
            DateTime endDate = DateTime.UtcNow;
            if (config.startDate != 0)
                startDate = DateTime.UtcNow.AddDays(config.startDate * -1);
            if (config.endDate != 0)
                endDate = DateTime.UtcNow.AddDays(config.startDate * -1);

            try
            {
                // Convert the string report type to the enum.
                ReportTypes reportType;
                Enum.TryParse<ReportTypes>(config.downloadDocumentType, out reportType);

                var report = amazonConnection.Reports.CreateReportAndDownloadFile(reportType, startDate, endDate, null, config.PII, null, 1000);

                var destFile = Path.Combine(config.documentDownloadFolder, config.downloadDocumentFileName);

                // The library downloads the file to the windows user temp folder so we have to move it.
                // Check if the file exists already and delete it.
                if (!String.IsNullOrEmpty(report))
                {
                    if (File.Exists(destFile))
                        File.Delete(destFile);

                    File.Move(report, destFile);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error downloading Report: " + config.downloadDocumentType + " " + e.ToString());
                logger.Debug("Error downloading Report: " + config.downloadDocumentType + " " + e.ToString());
                return "Failed";
            }

            return "Success";
        }
    }
}
