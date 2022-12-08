using FikaAmazonAPI;
using FikaAmazonAPI.Utils;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            // Load the config settings
            var config = new Config();
            try
            {
                config = JsonSerializer.Deserialize<Config>(File.ReadAllText("Config.json"));
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
            if (!String.IsNullOrEmpty(config.downloadDocumteType))
            {
                //var successful = DownloadDocumentSwitcher(amazonConnection, config.downloadDocumteType, config.documentDownloadFolder, config.downloadDocumentFileName);
                //logger.Info("Downloading document: " + config.downloadDocumteType + " Result: " + successful);
            }

            // Check if there is a upload document type specified.
            // If there is we scan the specified folder and upload ALL documents in that folder.
            if (!String.IsNullOrEmpty(config.uploadDocumentType))
            {
                logger.Info("Scanning upload documents folder.");
                var successful = UploadDocumentSwitcher(amazonConnection, config.uploadDocumentType, config.documentUploadFolder, config.documentUploadCompletedFolder, config.documentUploadFailedFolder);
                logger.Info("Uploading documents: " + config.uploadDocumentType + " Result: " + successful);
            }

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
        private static string DownloadDocumentSwitcher(AmazonConnection amazonConnection, string downloadDocumteType, string documentDownloadFolder, string downloadDocumentFileName)
        {
            switch (downloadDocumteType)
            {
                case "GET_FLAT_FILE_ACTIONABLE_ORDER_DATA_SHIPPING":
                case "GET_FLAT_FILE_ALL_ORDERS_DATA_BY_LAST_UPDATE_GENERAL":
                case "GET_FLAT_FILE_ALL_ORDERS_DATA_BY_ORDER_DATE_GENERAL":
                case "GET_FLAT_FILE_ARCHIVED_ORDERS_DATA_BY_ORDER_DATE":
                case "GET_FLAT_FILE_GEO_OPPORTUNITIES":
                case "GET_FLAT_FILE_MFN_SKU_RETURN_ATTRIBUTES_REPORT":
                case "GET_FLAT_FILE_OPEN_LISTINGS_DATA":
                case "GET_FLAT_FILE_ORDER_REPORT_DATA_SHIPPING":
                case "GET_FLAT_FILE_ORDERS_RECONCILIATION_DATA_SHIPPING":
                case "GET_FLAT_FILE_RETURNS_DATA_BY_RETURN_DATE":
                    return DownloadOrderOrderReport(amazonConnection, downloadDocumteType, documentDownloadFolder, downloadDocumentFileName);

                default:
                    return "Failed: Invalid document type.";
            }
        }

        private static string UploadDocumentSwitcher(AmazonConnection amazonConnection, string uploadDocumentType, string documentUploadFolder, string documentUploadCompletedFolder, string documentUploadFailedFolder)
        {
            switch (uploadDocumentType)
            {
                case "POST_FLAT_FILE_PRICEANDQUANTITYONLY_UPDATE_DATA":
                    return UploadFlatFilePriceAndQuantityDocument(amazonConnection, uploadDocumentType, documentUploadFolder, documentUploadCompletedFolder, documentUploadFailedFolder);

                default:
                    return "Failed: Invalid document type.";
            }
        }

        private static string UploadFlatFilePriceAndQuantityDocument(AmazonConnection amazonConnection, string uploadDocumentType, string documentUploadFolder, string documentUploadCompletedFolder, string documentUploadFailedFolder)
        {
            // Scan the upload document folder for files.
            if (Directory.Exists(documentUploadFolder))
            {
                string[] files = Directory.GetFiles(documentUploadFolder);

                foreach (var file in files)
                {
                    // We need to create a valid uri from the local path to pass to the library.
                    var feedID = amazonConnection.Feed.SubmitFeed(file, FeedType.POST_FLAT_FILE_PRICEANDQUANTITYONLY_UPDATE_DATA, null, null, ContentType.TXT);

                    Thread.Sleep(1000 * 30);
                    var feedOutput = amazonConnection.Feed.GetFeed(feedID);
                    //var outPut = amazonConnection.Feed.GetFeedDocument(feedOutput.ResultFeedDocumentId);
                    //var reportOutpit = outPut.Url;
                    //var processingReport = amazonConnection.Feed.GetFeedDocumentProcessingReport(outPut.Url);

                    while (feedOutput.ProcessingStatus == FikaAmazonAPI.AmazonSpApiSDK.Models.Feeds.Feed.ProcessingStatusEnum.INPROGRESS)
                    {
                        Console.WriteLine("Monitoring status of upload feed id: " + feedID + " Status: " + feedOutput.ProcessingStatus);
                        Thread.Sleep(1000 * 30);
                        feedOutput = amazonConnection.Feed.GetFeed(feedID);
                    }

                    Console.WriteLine("Uploading file: " + Path.GetFileName(file) + " Results: " + feedOutput.ProcessingStatus + " Feed ID: " + feedID);
                    logger.Info("Uploading file: " + Path.GetFileName(file) + " Results: " + feedOutput.ProcessingStatus + " Feed ID: " + feedID);
                }
            }
            else
            {
                logger.Debug("Failed: Upload documents folder does not exist: " + documentUploadFolder);
                return "Failed: Upload documents folder does not exist: " + documentUploadFolder;
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
        private static string DownloadOrderOrderReport(AmazonConnection amazonConnection, string downloadDocumteType, string documentDownloadFolder, string downloadDocumentFileName)
        {
            try
            {
                // Convert the string report type to the enum.
                ReportTypes reportType;
                Enum.TryParse<ReportTypes>(downloadDocumteType, out reportType);

                var report = amazonConnection.Reports.CreateReportAndDownloadFile(reportType, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, null, true, null, 1000);

                var destFile = documentDownloadFolder + downloadDocumentFileName;

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
                Console.WriteLine("Error downloading Order Report: " + e);
                logger.Debug("Error downloading Order Report: " + e);
                return "Failed";
            }

            return "Success";
        }
    }
}
