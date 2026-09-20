#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using TMPro;

public static class LandscapeThemeImporter
{
    const string Root = "Assets/UI/Updated UI/";
    const string Pack = Root + "hooligan-manager-sprites/";
    struct Slice
    {
        public string name; public int x,y,w,h,border;
        public Slice(string n, int x, int y, int w, int h, int border=0) { name=n; this.x=x; this.y=y; this.w=w; this.h=h; this.border=border; }
    }
    public static LandscapeTheme Import()
    {
        var path = "Assets/Resources/LandscapeTheme.asset";
        var theme = AssetDatabase.LoadAssetAtPath<LandscapeTheme>(path);
        if (!theme) { theme = ScriptableObject.CreateInstance<LandscapeTheme>(); AssetDatabase.CreateAsset(theme, path); }
        theme.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        theme.mainBackground = Single(Root+"bg_main_menu.png");
        theme.townBackground = Single(Root+"bg_town_clean.png");
        theme.tacticalBackground = Single(Root+"bg_tactical.png");
        theme.panel = Cropped(Pack+"panels/blank_trimmed/panel_dark_blank.png", "HM_PanelDark", 10, 10, 1482, 879, 18);
        theme.card = Cropped(Pack+"panels/blank_trimmed/panel_character_card_blank.png", "HM_CharacterCard", 10, 10, 787, 1356, 16);
        theme.redButton = Cropped(Pack+"buttons/blank_trimmed/btn_primary_red_blank.png", "HM_ButtonRed", 197, 313, 1141, 401, 14);
        theme.greenButton = Cropped(Pack+"buttons/blank_trimmed/btn_primary_green_blank.png", "HM_ButtonGreen", 151, 335, 1228, 338, 14);
        theme.darkButton = Cropped(Pack+"buttons/blank_trimmed/btn_secondary_dark_blank.png", "HM_ButtonDark", 98, 256, 1340, 508, 14);
        theme.outlineButton = Cropped(Pack+"buttons/blank_trimmed/btn_outline_dark_blank.png", "HM_ButtonOutline", 104, 350, 1327, 325, 14);
        var menu = Sheet(Root+"ChatGPT Image Sep 12, 2026, 06_42_34 PM (1).png", new[] {
            new Slice("HM_Logo",35,74,505,225),
            new Slice("HM_Cash",655,183,66,54), new Slice("HM_Crew",792,181,58,50),
            new Slice("HM_Star",925,177,65,63), new Slice("HM_Shield",1067,179,51,56),
            new Slice("HM_Settings",1310,174,66,65)
        });
        theme.logo=menu["HM_Logo"]; theme.cash=menu["HM_Cash"]; theme.crew=menu["HM_Crew"];
        theme.star=menu["HM_Star"]; theme.shield=menu["HM_Shield"]; theme.settings=menu["HM_Settings"];
        var members = Sheet(Root+"ChatGPT Image Sep 12, 2026, 06_42_36 PM (4).png", new[] {
            new Slice("HM_Portrait_0",319,174,201,153), new Slice("HM_Portrait_1",546,174,200,153),
            new Slice("HM_Portrait_2",770,174,200,153), new Slice("HM_Portrait_3",990,174,198,153),
            new Slice("HM_Portrait_4",1208,174,200,153)
        });
        theme.portraits=Enumerable.Range(0,5).Select(i=>members["HM_Portrait_"+i]).ToArray();
        var result = Sheet(Root+"ChatGPT Image Sep 12, 2026, 06_42_40 PM (6).png", new[] {
            new Slice("HM_VictoryEmblem",548,354,365,320), new Slice("HM_Celebration",18,5,882,337)
        });
        theme.emblem=result["HM_VictoryEmblem"]; theme.celebration=result["HM_Celebration"];
        theme.energy=Single(Pack+"icons/icon_energy.png");
        theme.lootBat=Single(Pack+"icons/icon_loot_bat.png");
        theme.lootBattery=Single(Pack+"icons/icon_loot_battery.png");
        theme.medkit=Single(Pack+"icons/icon_loot_medkit.png");
        theme.navHome=Single(Pack+"nav/nav_home.png");
        theme.navAttack=Single(Pack+"nav/nav_attack.png");
        theme.navSquad=Single(Pack+"nav/nav_squad.png");
        theme.navMissions=Single(Pack+"nav/nav_sidebar_missions.png");
        theme.taxiPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ithappy/Megacity/Traffic/Prefabs/Cars/car_004.prefab");
        theme.rivalVehiclePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ithappy/Megacity/Traffic/Prefabs/Cars/car_013.prefab");
        theme.vanPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ithappy/Megacity/Traffic/Prefabs/Cars/cargo_car_001.prefab");
        EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets(); return theme;
    }
    static Sprite Single(string path, int border=0)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new InvalidOperationException("Missing supplied artwork: "+path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.spriteBorder=new Vector4(border,border,border,border); Configure(importer); importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static Sprite Cropped(string path, string name, int x, int y, int w, int h, int border=0)
    {
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new InvalidOperationException("Missing supplied artwork: "+path);
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Multiple; Configure(importer);
#pragma warning disable 618
        importer.spritesheet=new[] {
            new SpriteMetaData {
                name=name,
                rect=new Rect(x,height-y-h,w,h),
                alignment=0,
                pivot=new Vector2(.5f,.5f),
                border=new Vector4(border,border,border,border)
            }
        };
#pragma warning restore 618
        importer.SaveAndReimport();
        var sprite=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(s=>s.name==name);
        if(!sprite) throw new InvalidOperationException("Failed to import sprite slice: "+name);
        return sprite;
    }
    static void Configure(TextureImporter i)
    {
        i.mipmapEnabled=false; i.alphaIsTransparency=true; i.filterMode=FilterMode.Bilinear;
        i.wrapMode=TextureWrapMode.Clamp; i.maxTextureSize=2048; i.textureCompression=TextureImporterCompression.Uncompressed;
        i.npotScale=TextureImporterNPOTScale.None;
    }
    static Dictionary<string,Sprite> Sheet(string path, Slice[] slices)
    {
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new InvalidOperationException("Missing sprite sheet: "+path);
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Multiple; Configure(importer);
        // Keep the user's existing slices. Add stable, named crops for the new UI.
#pragma warning disable 618
        var metadata = new List<SpriteMetaData>(importer.spritesheet);
        metadata.RemoveAll(m=>m.name.StartsWith("HM_", StringComparison.Ordinal));
        foreach (var s in slices)
            metadata.Add(new SpriteMetaData { name=s.name, rect=new Rect(s.x,height-s.y-s.h,s.w,s.h), alignment=0, pivot=new Vector2(.5f,.5f), border=new Vector4(s.border,s.border,s.border,s.border) });
        importer.spritesheet=metadata.ToArray();
#pragma warning restore 618
        importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Where(s=>s.name.StartsWith("HM_")).ToDictionary(s=>s.name);
    }
}
#endif
