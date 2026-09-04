using System.IO;
using UnityEngine;

namespace Pastoral.ImageResizer
{
    public static class ImageResizer
    {
        public static byte[] CompressAndResizeImage(byte[] imageBytes, int targetWidth, int targetHeight, int targetSizeKB)
        {
            // Load the image into a Texture2D
            Texture2D originalTexture = new Texture2D(2, 2);
            if (!originalTexture.LoadImage(imageBytes))
            {
                Debug.LogError("Failed to load image.");
                return null;
            }

            // Check if resizing is needed based on aspect ratio
            Texture2D processedTexture;
            if (NeedsResize(originalTexture.width, originalTexture.height, targetWidth, targetHeight))
            {
                processedTexture = ResizeTexture(originalTexture, targetWidth, targetHeight);
            }
            else
            {
                processedTexture = originalTexture; // No resizing needed
            }

            // Compress the texture to meet the target size
            byte[] compressedBytes = CompressToTargetSize(processedTexture, targetSizeKB);

            return compressedBytes;
        }

        private static bool NeedsResize(int originalWidth, int originalHeight, int targetWidth, int targetHeight)
        {
            float originalAspect = (float)originalWidth / originalHeight;
            float targetAspect = (float)targetWidth / targetHeight;
            return !Mathf.Approximately(originalAspect, targetAspect);
        }

        private static Texture2D ResizeTexture(Texture2D originalTexture, int width, int height)
        {
            Texture2D resizedTexture = new Texture2D(width, height, originalTexture.format, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)width;
                    float v = y / (float)height;
                    resizedTexture.SetPixel(x, y, originalTexture.GetPixelBilinear(u, v));
                }
            }
            resizedTexture.Apply();
            return resizedTexture;
        }

        private static byte[] CompressToTargetSize(Texture2D texture, int targetSizeKB)
        {
            int quality = 100; // Start with the highest quality
            byte[] compressedBytes;

            do
            {
                compressedBytes = texture.EncodeToJPG(quality);
                quality -= 5; // Reduce quality incrementally
            } while (compressedBytes.Length > targetSizeKB * 1024 && quality > 5);

            if (quality <= 5)
            {
                Debug.LogWarning("Could not achieve the target size. Using the smallest possible file.");
            }

            return compressedBytes;
        }
    }
}
