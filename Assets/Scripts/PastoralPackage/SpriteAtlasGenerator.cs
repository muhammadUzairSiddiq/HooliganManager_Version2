using UnityEngine;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Pastoral.SpriteEditor{

public class SpriteAtlasGenerator : MonoBehaviour
{
    public bool GeneratorSpriteAtlasOnEnable;
    private void Start() {
        if(GeneratorSpriteAtlasOnEnable)
            GeneratorSpriteAtlas();
    }
    public bool IsGeneratingAtlas;
    public List<Sprite> SpritesToCreateAtlasOf; // List of sprites to include in the atlas
    public string SpritesToCreateAtlasOf_FolderPath; // List of sprites to include in the atlas
    public void MakeSpritesReadable(){
#if UNITY_EDITOR
        Debug.Log($"SpritesToCreateAtlasOf_FolderPath = {SpritesToCreateAtlasOf_FolderPath}");
        foreach (Sprite sprite in SpritesToCreateAtlasOf)
        {
            TextureImporter textureImporter = AssetImporter.GetAtPath(SpritesToCreateAtlasOf_FolderPath+'/'+sprite.name+".png") as TextureImporter;
            if(!textureImporter.isReadable){
                textureImporter.isReadable = true;
                textureImporter.SaveAndReimport();
            }
        }
#endif
    }
    [HideInInspector]public string atlasName = "SpriteAtlas"; // Name for the atlas texture
    [Tooltip("Set global path of the directory you want the atlas sprite to be saved")] 
    public string PathToSaveSpriteTo;

    public void GeneratorSpriteAtlas()
    {
        IsGeneratingAtlas = true;
#if UNITY_EDITOR
        if(PathToSaveSpriteTo== null) {
            Debug.LogError("PathToSaveSpriteTo is null.");
            return;
        }
#endif
        if (SpritesToCreateAtlasOf == null || SpritesToCreateAtlasOf.Count == 0)
        {
            Debug.Log("No SpritesToCreateAtlasOf provided for atlas generation.");
            return;
        }
        MakeSpritesReadable();

        // Determine the appropriate atlas size
        int atlasSize = DetermineAtlasSize(SpritesToCreateAtlasOf);

        // Convert the list of sprites to a list of textures
        List<Texture2D> textures = new List<Texture2D>();
        foreach (var sprite in SpritesToCreateAtlasOf)
        {
            Texture2D texture = SpriteToTexture2D(sprite);
            if (texture != null)
            {
                textures.Add(texture);
            }
        }

        // Create the atlas
        Texture2D atlasTexture = new Texture2D(atlasSize, atlasSize);
        Rect[] rects = atlasTexture.PackTextures(textures.ToArray(), 2, atlasSize);

        // Save the atlas texture
        string atlasPath = SaveAtlasTexture(atlasTexture);

#if UNITY_EDITOR
        // Create sprite metadata for each sprite in the atlas
        List<SpriteMetaData> metaData = new List<SpriteMetaData>();
        for (int i = 0; i < rects.Length; i++)
        {
            Sprite sprite = SpritesToCreateAtlasOf[i];
            Rect rect = rects[i];

            SpriteMetaData smd = new SpriteMetaData
            {
                alignment = (int)SpriteAlignment.Center,
                border = sprite.border,
                name = sprite.name,
                pivot = sprite.pivot,
                rect = new Rect(rect.x * atlasTexture.width, rect.y * atlasTexture.height, rect.width * atlasTexture.width, rect.height * atlasTexture.height)
            };

            metaData.Add(smd);
        }

        // Import the texture as a sprite with multiple mode
        ImportTextureAsSprite(atlasPath, metaData.ToArray());
#endif
        IsGeneratingAtlas = false;
    }

    private Texture2D SpriteToTexture2D(Sprite sprite)
    {
        if (sprite == null) return null;

        // Create a new Texture2D from the sprite's texture
        Texture2D texture = new Texture2D((int)sprite.textureRect.width, (int)sprite.textureRect.height);
        Debug.Log($"sprite.name:{sprite.name}");
        Debug.Log($"SpriteToTexture2D atlas:{(int)sprite.rect.width}, {(int)sprite.rect.height}");
        Color[] pixels = sprite.texture.GetPixels((int)sprite.textureRect.x, (int)sprite.textureRect.y, (int)sprite.textureRect.width, (int)sprite.textureRect.height);
        Debug.Log($"SpriteToTexture2D pixels:{pixels.Length}");
        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    private string SaveAtlasTexture(Texture2D atlasTexture)
    {
        byte[] bytes = atlasTexture.EncodeToPNG();
        string path = Path.Combine(PathToSaveSpriteTo, atlasName + ".png");
        File.WriteAllBytes(path, bytes);
        Debug.Log("Atlas saved to: " + path);

#if UNITY_EDITOR
        // Refresh the asset database to see the new texture in the editor
        UnityEditor.AssetDatabase.Refresh();
#endif
        Debug.Log(PathToSaveSpriteTo.Split("Assets\\")[0].Replace('\\', '/')+'/' + atlasName + ".png");
        return  PathToSaveSpriteTo.Split("Assets\\")[0].Replace('\\', '/')+'/' + atlasName + ".png";
        // return Application.streamingAssetsPath + atlasName + ".png";
    }

#if UNITY_EDITOR
    private void ImportTextureAsSprite(string texturePath, SpriteMetaData[] metaData)
    {
        // Load the texture asset
        TextureImporter textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (textureImporter == null)
        {
            Debug.LogError("Failed to load texture importer for path: " + texturePath);
            return;
        }

        Debug.Log($"TextureImporter found for path: {texturePath}");

        // Set the texture type to sprite and enable multiple sprite mode
        textureImporter.textureType = TextureImporterType.Sprite;
        textureImporter.spriteImportMode = SpriteImportMode.Multiple;
        textureImporter.spritesheet = metaData;

        // Apply the changes
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
        Debug.Log("Atlas texture imported with multiple sprites at path: " + texturePath);
    }
#endif

    private int DetermineAtlasSize(List<Sprite> SpritesToCreateAtlasOf)
    {
        // Calculate the total area of all SpritesToCreateAtlasOf
        float totalArea = 0;
        foreach (var sprite in SpritesToCreateAtlasOf)
        {
            totalArea += sprite.rect.width * sprite.rect.height;
        }

        // Find the smallest power of 2 size that can accommodate the total area
        int size = Mathf.CeilToInt(Mathf.Sqrt(totalArea));
        int atlasSize = 1;
        while (atlasSize < size)
        {
            atlasSize *= 2;
        }

        Debug.Log($"Determined atlas size: {atlasSize} x {atlasSize}");
        // Ensure the atlas size is not too small
        atlasSize = Mathf.Max(atlasSize, 256); // Minimum size of 256

        // Clamp to a reasonable maximum size
        atlasSize = Mathf.Min(atlasSize, 8192); // Maximum size of 8192

        Debug.Log($"Final Determined atlas size: {atlasSize} x {atlasSize}");

        return atlasSize;
    }
}
}
