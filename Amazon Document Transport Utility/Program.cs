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

            // Check for a command line argument.
            // We allow one argument which is a json doc with documents to overwrite the base Config.json
            // This allows us to use the same AMDU for multiple disparate purposes without making copies of the applicatiopn.
            if (args.Count() > 0 
                && args[0] == "-document"
                && args[1].Contains(".json")) 
            {
                try
                {
                    Console.WriteLine("Loading command line documents file: " + args[1]);
                    var documents = JsonSerializer.Deserialize<List<Document>>(File.ReadAllText(args[1]));
                    config.Documents.Clear();
                    config.Documents = documents;
                    config.Continuous = false; // Turn off continuious mode for command line document configs.

                    Console.WriteLine("Documents found: " + config.Documents.Count);
                }
                catch (Exception e )
                {
                    logger.Debug("Error loading command line documents json: " + e.ToString());
                    return;
                }
            }


            // Setup the SP-API connection configuration class.
            AmazonConnection amazonConnection = new AmazonConnection(new AmazonCredential()
            {
                AccessKey = config.AccessKey,
                SecretKey = config.SecretKey,
                RoleArn = config.RoleArn,
                ClientId = config.ClientId,
                ClientSecret = config.ClientSecret,
                RefreshToken = config.RefreshToken,
                MarketPlace =  MarketPlace.US //MarketPlace.GetMarketplaceByCountryCode(config.marketplace),
            });

            do
            {
                Console.WriteLine("Processing documents: " + DateTime.Now.ToShortTimeString());

                // Cycle through the document array and run all the downloads and uploads.
                foreach (var document in config.Documents)
                {
                    // Check if there is a download document type specified.
                    if (!String.IsNullOrEmpty(document.DownloadDocumentType))
                    {
                        var successful = DownloadDocumentSwitcher(amazonConnection, document);
                    }

                    // Check if there is a upload document type specified.
                    // If there is we scan the specified folder and upload ALL documents in that folder.
                    if (!String.IsNullOrEmpty(document.UploadDocumentType))
                    {
                        var successful = UploadDocumentSwitcher(amazonConnection, document);
                    }
                }

                // Warn the user you not close the window if running i continues mode.
                if (config.Continuous)
                {
                    Console.WriteLine("\nContinues mode is active. Running again in " + config.ContinuousSeconds + " seconds.");
                    Console.WriteLine("DO NOT CLOSE THIS WINDOW!\n");
                    Thread.Sleep(config.ContinuousSeconds * 1000); // The config is in seconds so convert to milliseconds.
                }

                // Flush the log manager buffer after every cycle of the documents.
                LogManager.Flush();

            } while (config.Continuous == true);


            // Explicitly close the logger so it clears the buffers. If you leave it to chance sometimes it will not purge the buffer and close the logs.
            LogManager.Shutdown();
        }

        /// <summary>
        /// There is support for a lot of different document types to download. We have to send specific types to their respective functions to download.
        /// </summary>
        /// <param name="amazonConnection"></param>
        /// <param name="downloadDocumentType"></param>
        /// <param name="documentDownloadFolder"></param>
        /// <param name="downloadDocumentFileName"></param>
        /// <returns></returns>
        private static string DownloadDocumentSwitcher(AmazonConnection amazonConnection, Document document)
        {
            switch (document.DownloadDocumentType)
            {
                case "GET_FLAT_FILE_ACTIONABLE_ORDER_DATA_SHIPPING":
                case "GET_FLAT_FILE_ALL_ORDERS_DATA_BY_ORDER_DATE_GENERAL":
                case "GET_FLAT_FILE_ORDER_REPORT_DATA_SHIPPING":
                case "GET_AMAZON_FULFILLED_SHIPMENTS_DATA_GENERAL":
                case "GET_FLAT_FILE_RETURNS_DATA_BY_RETURN_DATE":
                case "GET_REFERRAL_FEE_PREVIEW_REPORT":
                    return DownloadFlatFileOrderReport(amazonConnection, document);

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
        private static string UploadDocumentSwitcher(AmazonConnection amazonConnection, Document document)
        {
            switch (document.UploadDocumentType)
            {
                case "POST_FLAT_FILE_PRICEANDQUANTITYONLY_UPDATE_DATA":
                case "POST_FLAT_FILE_FULFILLMENT_DATA":
                    return UploadFlatFileFeed(amazonConnection, document);
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
        private static string UploadFlatFileFeed(AmazonConnection amazonConnection, Document document)
        {
            // Scan the upload document folder for files.
            if (Directory.Exists(document.DocumentUploadFolder))
            {
                logger.Info("Scanning upload documents folder.");

                try
                {
                    string[] files = Directory.GetFiles(document.DocumentUploadFolder);

                    // Convert the string report type to the enum.
                    FeedType reportType;
                    Enum.TryParse<FeedType>(document.UploadDocumentType, out reportType);

                    foreach (var file in files)
                    {
                        Console.WriteLine("Starting to upload file: " + Path.GetFileName(file));
                        logger.Info("Starting to upload file: " + Path.GetFileName(file));
                        var feedID = amazonConnection.Feed.SubmitFeed(file, reportType, null, null, ContentType.TXT, ContentFormate.File);

                        // Wait some time so we can get a Feed ID
                        Thread.Sleep(1000 * 30);
                        var feedOutput = amazonConnection.Feed.GetFeed(feedID);

                        // Once the file is INPROGRESS we move it to the finished file. We don't wait to see if it was successful or not.
                        // Some inventory files are huge. Waiting on them to upload before moving on would be detrimental.
                        if (!String.IsNullOrEmpty(feedOutput.FeedId))
                        {
                            Console.WriteLine("Uploading file: " + Path.GetFileName(file) + " Feed ID: " + feedID);
                            logger.Info("Uploading file: " + Path.GetFileName(file) + " Feed ID: " + feedID);
                            File.Move(file, Path.Combine(document.DocumentUploadCompletedFolder, Path.GetFileName(file) + DateTime.Now.ToString("_yyyy-MM-dd_HHmmssffff")));
                        }
                        else
                        {
                            File.Move(file, Path.Combine(document.DocumentUploadFailedFolder, Path.GetFileName(file) + DateTime.Now.ToString("_yyyy-MM-dd_HHmmssffff")));
                            logger.Debug("Failed to upload flat file feed: " + document.UploadDocumentType + " " + file);
                        }

                        //while (feedOutput.ProcessingStatus == FikaAmazonAPI.AmazonSpApiSDK.Models.Feeds.Feed.ProcessingStatusEnum.INPROGRESS)
                        //{
                            //Console.WriteLine("Monitoring status of uploaded feed id: " + feedID + " Status: " + feedOutput.ProcessingStatus);
                            //Thread.Sleep(1000 * 30);
                            //feedOutput = amazonConnection.Feed.GetFeed(feedID);
                        //}
                    }
                }
                catch (Exception e)
                {
                    logger.Info("Error uploading flat file feed: " + document.UploadDocumentType + " " + e.ToString());
                    logger.Debug("Error uploading flat file feed: " + document.UploadDocumentType + " " + e.ToString());
                    return "Failed";
                }
            }
            else
            {
                logger.Info("Failed: Upload documents folder does not exist: " + document.DocumentUploadFolder);
                logger.Debug("Failed: Upload documents folder does not exist: " + document.DocumentUploadFolder);
                return "Failed: Upload documents folder does not exist: " + document.DocumentUploadFolder;
            }

            return "Success";
        }

        /// <summary>
        /// Function to download flat file report types. 
        /// </summary>
        /// <param name="amazonConnection"></param>
        /// <param name="downloadDocumentType"></param>
        /// <param name="documentDownloadFolder"></param>
        /// <param name="downloadDocumentFileName"></param>
        /// <returns></returns>
        private static string DownloadFlatFileOrderReport(AmazonConnection amazonConnection, Document document)
        {

            // Allow start and end dates. In the config file we have positive numbers but convert them to negatives to go backwards.
            DateTime startDate = DateTime.UtcNow.AddDays(-30);
            DateTime endDate = DateTime.UtcNow;
            if (document.StartDate != 0)
                startDate = DateTime.UtcNow.AddDays(document.StartDate * -1);
            if (document.EndDate != 0)
                endDate = DateTime.UtcNow.AddDays(document.StartDate * -1);

            try
            {
                // Convert the string report type to the enum.
                ReportTypes reportType;
                Enum.TryParse<ReportTypes>(document.DownloadDocumentType, out reportType);

                var report = amazonConnection.Reports.CreateReportAndDownloadFile(reportType, startDate, endDate, null, document.PII, null, 1000);

                // The library downloads the file to the windows user temp folder so we have to move it.
                // Check if the file exists already and delete it.
                if (!String.IsNullOrEmpty(report))
                {
                    var destFile = "";

                    if (document.AppendTimeStamp)
                    {
                        // Append timestamp to the resulting downloaded report.
                        destFile = Path.Combine(document.DocumentDownloadFolder, Path.GetFileNameWithoutExtension(document.DownloadDocumentFileName) + 
                                                                                 DateTime.Now.ToString("_yyyy-MM-dd_HHmmssffff") + 
                                                                                 Path.GetExtension(document.DownloadDocumentFileName));
                    }
                    else
                    {
                        destFile = Path.Combine(document.DocumentDownloadFolder, document.DownloadDocumentFileName);
                    }

                    if (File.Exists(destFile))
                        File.Delete(destFile);

                    File.Move(report, destFile);

                    logger.Info("Downloading document: " + document.DownloadDocumentType + " Result: Success");
                    Console.WriteLine("Downloading document: " + document.DownloadDocumentType + " Result: Success");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error downloading Report: " + document.DownloadDocumentType + " " + e.ToString());
                logger.Debug("Error downloading Report: " + document.DownloadDocumentType + " " + e.ToString());
                return "Failed";
            }

            return "Success";
        }
    }
}
