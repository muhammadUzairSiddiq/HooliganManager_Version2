using UnityEngine;
using System.IO;

public class RenderTextureToPNG : MonoBehaviour
{
    public RenderTexture renderTexture;
    public string savePath = "Assets/SavedImages/";

    private void Start()
    {
        SaveRenderTextureToPNG();
    }
    public void SaveRenderTextureToPNG()
    {
        Texture2D tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        RenderTexture.active = renderTexture;
        tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        RenderTexture.active = null;

        byte[] bytes = tex.EncodeToPNG();
        Destroy(tex);

        string filePath = @"D:\Unity\Projects\PokeIntheI\Assets\Output.png";
        File.WriteAllBytes(filePath, bytes);
    }
}
