using UnityEngine;
public class CombineTextureInfo
{
    public Rect[] uvRect = new Rect[4];    
    public Texture2D texture;

    public CombineTextureInfo(Texture2D texture,Rect RRect, Rect GRect, Rect BRect, Rect ARect)
    {
        this.uvRect = new Rect[] { RRect, GRect, BRect, ARect };
        this.texture = texture;
    }
    public CombineTextureInfo(Texture2D texture,Rect RRect)
    {
        Rect GRect= RRect; Rect BRect= RRect; Rect ARect = RRect;
        this.uvRect = new Rect[] { RRect, GRect, BRect, ARect };
        this.texture = texture;
    }
    public CombineTextureInfo(Texture2D texture,  Rect RRect, Rect GRect)
    {
        Rect BRect = GRect; Rect ARect = GRect;
        this.uvRect = new Rect[] { RRect, GRect, BRect, ARect };
        this.texture = texture;
    }
}

public interface ITextureCombiner
{
    /// <summary>
    /// 合併貼圖
    /// </summary>
    /// <param name="sourceTextures">來源貼圖資訊</param>
    /// <param name="combinedTexture">輸出貼圖</param>
    /// <returns>合併結果</returns>
    bool Combine(CombineTextureInfo[] sourceTextures,int textureSize, ref RenderTexture combinedTexture);
}
