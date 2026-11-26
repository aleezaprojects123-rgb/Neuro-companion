using UnityEngine;
using UnityEditor;

namespace NeuroCompanion.Backend.Editor
{
    public static class SkyTextureAnalyzer
    {
        [MenuItem("NeuroCompanion/Analyze Sky Texture")]
        public static void Analyze()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Environment/GardenSky_Realistic_Equirectangular.png");
            if (tex == null)
            {
                Debug.LogError("Sky texture not found!");
                return;
            }

            Debug.Log($"Sky texture: {tex.width}x{tex.height}, format: {tex.format}");

            // Make readable if needed
            string path = AssetDatabase.GetAssetPath(tex);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            // Find bright spots
            var pixels = tex.GetPixels();
            float maxB = 0f;
            int maxIdx = 0;
            int countBright = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                float b = pixels[i].grayscale;
                if (b > maxB) { maxB = b; maxIdx = i; }
                if (b > 0.95f) countBright++;
            }

            int maxY = maxIdx / tex.width;
            int maxX = maxIdx % tex.width;
            Debug.Log($"Max Brightness: {maxB} at pixel ({maxX}, {maxY}) out of ({tex.width}, {tex.height}). Total > 0.95: {countBright}");

            // Latitude and Longitude of brightest spot
            float u = (float)maxX / tex.width;
            float v = (float)maxY / tex.height;
            float yawDeg = u * 360f;
            float pitchDeg = (v - 0.5f) * 180f;
            Debug.Log($"Brightest spot in equirectangular: Yaw = {yawDeg} deg, Pitch = {pitchDeg} deg");
        }
    }
}
