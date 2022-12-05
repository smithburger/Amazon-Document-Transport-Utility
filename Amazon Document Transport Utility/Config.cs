﻿using System.Collections.Generic;

namespace Amazon_Document_Transport_Utility
{
    internal class Config
    {
        public string configName { get; set; }
        public bool continuous { get; set; }
        public string accessKey { get; set; }
        public string secretKey { get; set; }
        public string roleARN { get; set; }
        public string clinetId { get; set; }
        public string clientSecret { get; set; }
        public string refreshToken { get; set; }
        public string marketplace { get; set; }
        public string documentDownloadFolder { get; set; }
        public string documentUploadFolder { get; set; }
        public string documentCompletedFolder { get; set; }
        public string documentFailedFolder { get; set; }
        public string downloadDocumentFileName { get; set; }
    }
}
