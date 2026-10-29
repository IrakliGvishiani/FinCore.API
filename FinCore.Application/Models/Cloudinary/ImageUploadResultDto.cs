using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.Cloudinary
{
    public class ImageUploadResultDto
    {
        public string publicId { get; set; }
        public string Url { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public long Bytes { get; set; }
    }
}
