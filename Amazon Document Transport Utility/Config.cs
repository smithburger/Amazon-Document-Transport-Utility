using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Amazon_Document_Transport_Utility
{
    internal class Config
    {
        [JsonPropertyName("configName")]
        public string ConfigName { get; set; }

        [JsonPropertyName("continuous")]
        public bool Continuous { get; set; }

        [JsonPropertyName("continuousSeconds")]
        public int ContinuousSeconds { get; set; }

        [JsonPropertyName("accessKey")]
        public string AccessKey { get; set; }

        [JsonPropertyName("secretKey")]
        public string SecretKey { get; set; }

        [JsonPropertyName("roleARN")]
        public string RoleArn { get; set; }

        [JsonPropertyName("clientId")]
        public string ClientId { get; set; }

        [JsonPropertyName("clientSecret")]
        public string ClientSecret { get; set; }

        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("marketplace")]
        public string Marketplace { get; set; }

        [JsonPropertyName("documents")]
        public List<Document> Documents { get; set; }
    }

    internal class Document
    {
        [JsonPropertyName("documentDownloadFolder")]
        public string DocumentDownloadFolder { get; set; }

        [JsonPropertyName("documentUploadCompletedFolder")]
        public string DocumentUploadCompletedFolder { get; set; }

        [JsonPropertyName("documentUploadFailedFolder")]
        public string DocumentUploadFailedFolder { get; set; }

        [JsonPropertyName("documentFailedFolder")]
        public string DocumentFailedFolder { get; set; }

        [JsonPropertyName("downloadDocumentFileName")]
        public string DownloadDocumentFileName { get; set; }

        [JsonPropertyName("documentUploadFolder")]
        public string DocumentUploadFolder { get; set; }

        [JsonPropertyName("downloadDocumentType")]
        public string DownloadDocumentType { get; set; }

        [JsonPropertyName("uploadDocumentType")]
        public string UploadDocumentType { get; set; }

        [JsonPropertyName("startDate")]
        public int StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public int EndDate { get; set; }

        [JsonPropertyName("PII")]
        public bool PII { get; set; }
    }
}
