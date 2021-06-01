using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.IO;
using System.Linq;
using Funcy.Graphics;
using MyBox;
#if UNITY_EDITOR
using UnityEditor;
#endif
[ExecuteInEditMode]
public class CharacterCombine : MonoBehaviour, ITextureCombiner
{
    [System.Serializable]public class CombineGroup
    {
        public enum RenderMode
        {
            Meshrenderer, SkinnedMeshRenderer
        }

        public RenderMode renderMode = RenderMode.Meshrenderer;

        [ConditionalField(nameof(renderMode), false, RenderMode.Meshrenderer)] public MeshFilter filter;
        [ConditionalField(nameof(renderMode), false, RenderMode.Meshrenderer)] public MeshRenderer meshRenderer;

        [ConditionalField(nameof(renderMode), false, RenderMode.SkinnedMeshRenderer)] public SkinnedMeshRenderer skinnedMeshRenderer;
        [ReadOnly] public Material material;

        public CombineGroup(Renderer ren)
        {            
            if (ren.GetType().Name == typeof(SkinnedMeshRenderer).Name)
            {
                var r = ren as SkinnedMeshRenderer;
                this.renderMode = RenderMode.SkinnedMeshRenderer;
                skinnedMeshRenderer = r;
            }
            if (ren.GetType().Name == typeof(MeshRenderer).Name)
            {
                var r = ren as MeshRenderer;
                this.renderMode = RenderMode.Meshrenderer;
                filter = r.GetComponent<MeshFilter>();
                meshRenderer = r;
            }
            material = ren.sharedMaterial;
        }
        
        public bool IsActive { get { return ((filter != null && meshRenderer != null) || skinnedMeshRenderer != null) && material != null; } }
        
        public Mesh sharedMesh {
            get {
                if (skinnedMeshRenderer == null && meshRenderer == null) return null;
                if (this.renderMode == RenderMode.Meshrenderer)
                    return filter.sharedMesh;
                else
                    return skinnedMeshRenderer.sharedMesh;
            }
            set {
                if (this.renderMode == RenderMode.Meshrenderer)
                    filter.sharedMesh = value;
                else
                    skinnedMeshRenderer.sharedMesh = value;
            }
        }
                
        public Texture2D GetTexture2D(string name) { return (Texture2D)material.GetTexture(name); }
        public void SetTexture2D(string name, Texture2D value) { material.SetTexture(name, value); }

    }

    [HideInInspector] [SerializeField] List<CombineGroup> combineGroups = new List<CombineGroup>();
    [ReadOnly] [SerializeField] CombineGroup hair, face, body;
    public CombineGroup result;
    


    [ReadOnly] public ComputeShader computeShader;

    [HideInInspector] [SerializeField] RenderTexture diffuseResult;
    [HideInInspector] [SerializeField] RenderTexture essgMaskResult;
    [HideInInspector] [SerializeField] RenderTexture selfMaskResult;
    [HideInInspector] [SerializeField] RenderTexture outlineMaskResult;


    private void OnEnable()
    {    
        UpdateGroups();                
    }

    List<CombineGroup> GetCombineGroups(Transform root)
    {
        List<CombineGroup> result = new List<CombineGroup>();
        foreach (var ren in root.GetComponentsInChildren<Renderer>())
        {
            result.Add(new CombineGroup(ren));
        }
        return result;
    }

    public void DoCombine()
    {
        CombineMesh();
        CombineMaps();
        CombineMaterials();
    }

    public void UpdateGroups()
    {
        combineGroups = GetCombineGroups(transform);

        hair = combineGroups.Find(x => x.sharedMesh.name.ToLower().Contains("hair"));
        face = combineGroups.Find(x => x.sharedMesh.name.ToLower().Contains("face"));
        body = combineGroups.Find(x => x.sharedMesh.name.ToLower().Contains("body"));
    }
    private void OnDisable()
    {
        
    }

    private void Update()
    {
        
    }

    void SafeDestory(UnityEngine.Object o)
    {
        if (Application.isPlaying)
        {
            Destroy(o);
        }
        else
        {
            DestroyImmediate(o);
        }
    }
#if UNITY_EDITOR

    public void SaveMapsAndAssignToMaterial()
    {
        var rts = new RenderTexture[] { diffuseResult, essgMaskResult, selfMaskResult, outlineMaskResult };
        string path = EditorUtility.SaveFolderPanel("Save to png", Application.dataPath, "");
        foreach (var rt in rts)
        {
            string savePath = Path.Combine(path, rt.name + ".tga");

            SaveRenderTextureToTGA(savePath, rt, importer =>
            {
                var map = AssetDatabase.LoadAssetAtPath<Texture2D>(importer.assetPath);
                result.material.SetTexture(rt.name, map);
            });
        }

        var matPath = Path.Combine(path, result.material.name + ".mat").Replace(Application.dataPath, "Assets");
        var meshPath = Path.Combine(path, result.sharedMesh.name + ".asset").Replace(Application.dataPath, "Assets");
        AssetDatabase.DeleteAsset(matPath);
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(result.material, matPath);
        AssetDatabase.CreateAsset(result.sharedMesh, meshPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (result.renderMode == CombineGroup.RenderMode.SkinnedMeshRenderer)
        {
            result.skinnedMeshRenderer.sharedMaterial = mat;
            result.skinnedMeshRenderer.sharedMesh = mesh;
        }
        else
        {
            result.meshRenderer.sharedMaterial = mat;
            result.filter.sharedMesh = mesh;
        }
    }

    private void SaveRenderTextureToTGA(string path, RenderTexture renderTexture, System.Action<TextureImporter> importAction = null)
    {        
        if (path.Length != 0)
        {
            var newTex = new Texture2D(renderTexture.width, renderTexture.height);
            RenderTexture.active = renderTexture;
            newTex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            newTex.Apply();

            byte[] pngData = newTex.EncodeToTGA();
            if (pngData != null)
            {
                File.WriteAllBytes(path, pngData);                

                System.Action action = null;                
                AssetDatabase.Refresh();
                var importer = AssetImporter.GetAtPath(path.Replace(Application.dataPath, "Assets")) as TextureImporter;
                Debug.Log(importer == null);
                importAction?.Invoke(importer);
            }
            
        }
    }
#endif

    #region CombineMesh 
    public bool CombineMesh()
    {
        if (result.sharedMesh == null)
            result.sharedMesh = new Mesh();
        result.sharedMesh.Clear();

        result.sharedMesh.name = "Combined"; 
        

        Mesh hairCloneMesh = Instantiate(hair.sharedMesh);
        Mesh faceCloneMesh = Instantiate(face.sharedMesh);
        Mesh bodyCloneMesh = Instantiate(body.sharedMesh);
        

        int maxCount = math.max(math.max(hairCloneMesh.uv.Length, faceCloneMesh.uv.Length), bodyCloneMesh.uv.Length);

        Buffer hairUVBuffer = new Buffer(maxCount,typeof(Vector2));
        Buffer faceUVBuffer = new Buffer(maxCount, typeof(Vector2));
        Buffer bodyUVBuffer = new Buffer(maxCount, typeof(Vector2));

        hairUVBuffer.SetData(hairCloneMesh.uv);
        faceUVBuffer.SetData(faceCloneMesh.uv);
        bodyUVBuffer.SetData(bodyCloneMesh.uv);

        int kernel = computeShader.FindKernel("CombineMesh");
        uint x, y, z;
        computeShader.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
        computeShader.SetInt("hairUVLength", hairCloneMesh.uv.Length);
        computeShader.SetInt("faceUVLength", faceCloneMesh.uv.Length);
        computeShader.SetInt("bodyUVLength", bodyCloneMesh.uv.Length);
        computeShader.SetBuffer(kernel, "hairUV", hairUVBuffer.target);
        computeShader.SetBuffer(kernel, "faceUV", faceUVBuffer.target);
        computeShader.SetBuffer(kernel, "bodyUV", bodyUVBuffer.target);
        
        computeShader.Dispatch(kernel, Mathf.CeilToInt(maxCount / (float)x), Mathf.CeilToInt(y), Mathf.CeilToInt(z));

        Vector2[] hairUV = new Vector2[maxCount];
        Vector2[] faceUV = new Vector2[maxCount];
        Vector2[] bodyUV = new Vector2[maxCount];

        hairCloneMesh.uv2 = hairUV.ToList().GetRange(0, hairCloneMesh.uv.Length).ToArray();
        faceCloneMesh.uv2 = faceCloneMesh.uv;
        bodyCloneMesh.uv2 = bodyUV.ToList().GetRange(0, bodyCloneMesh.uv.Length).ToArray();

        hairUVBuffer.GetData(hairUV);
        faceUVBuffer.GetData(faceUV);
        bodyUVBuffer.GetData(bodyUV);

        hairCloneMesh.uv = hairUV.ToList().GetRange(0, hairCloneMesh.uv.Length).ToArray();
        faceCloneMesh.uv = faceUV.ToList().GetRange(0, faceCloneMesh.uv.Length).ToArray();
        bodyCloneMesh.uv = bodyUV.ToList().GetRange(0, bodyCloneMesh.uv.Length).ToArray();


        Matrix4x4 identity = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
        CombineInstance bodyMesh = new CombineInstance();
        bodyMesh.mesh = bodyCloneMesh;        
        bodyMesh.transform = result.renderMode == CombineGroup.RenderMode.SkinnedMeshRenderer ? body.skinnedMeshRenderer.localToWorldMatrix : identity;

        CombineInstance faceMesh = new CombineInstance();
        faceMesh.mesh = faceCloneMesh;
        faceMesh.transform = result.renderMode == CombineGroup.RenderMode.SkinnedMeshRenderer ? face.skinnedMeshRenderer.localToWorldMatrix : identity;

        CombineInstance hairMesh = new CombineInstance();
        hairMesh.mesh = hairCloneMesh;
        hairMesh.transform = result.renderMode == CombineGroup.RenderMode.SkinnedMeshRenderer ? hair.skinnedMeshRenderer.localToWorldMatrix : identity;




        CombineInstance[] combines = new CombineInstance[] { bodyMesh, faceMesh, hairMesh };
        result.sharedMesh.CombineMeshes(combines, true, true);



        if (result.renderMode == CombineGroup.RenderMode.SkinnedMeshRenderer)
        {
            List<Transform> bones = new List<Transform>();
            List<BoneWeight> boneWeights = new List<BoneWeight>();
            List<Matrix4x4> bindPoses = new List<Matrix4x4>();
            int boneOffset = 0;
            SkinnedMeshRenderer[] smRenderers = new SkinnedMeshRenderer[] { body.skinnedMeshRenderer, face.skinnedMeshRenderer, hair.skinnedMeshRenderer };
            for (int s = 0; s < smRenderers.Length; s++)
            {
                SkinnedMeshRenderer smr = smRenderers[s];

                BoneWeight[] meshBoneweight = smr.sharedMesh.boneWeights;

                // May want to modify this if the renderer shares bones as unnecessary bones will get added.

                for (int i = 0; i < meshBoneweight.Length; ++i)
                {
                    BoneWeight bWeight = meshBoneweight[i];

                    bWeight.boneIndex0 += boneOffset;
                    bWeight.boneIndex1 += boneOffset;
                    bWeight.boneIndex2 += boneOffset;
                    bWeight.boneIndex3 += boneOffset;
                     
                    boneWeights.Add(bWeight);
                }

                boneOffset += smr.bones.Length;


                for (int i = 0; i < smr.bones.Length; ++i)
                {
                    bones.Add(smr.bones[i]);

                    //we take the old bind pose that mapped from our mesh to world to bone,
                    //and take out our localToWorldMatrix, so now it's JUST the bone matrix
                    //since our skinned mesh renderer is going to be on the root of our object that works
                    bindPoses.Add(smr.sharedMesh.bindposes[i] * smr.transform.worldToLocalMatrix);
                }
                smr.enabled = false;
            }
            result.skinnedMeshRenderer.bones = bones.ToArray();
            result.sharedMesh.boneWeights = boneWeights.ToArray();
            result.sharedMesh.bindposes = bindPoses.ToArray();
            result.sharedMesh.RecalculateBounds();
        }

        DestroyImmediate(hairCloneMesh);
        DestroyImmediate(faceCloneMesh);
        DestroyImmediate(bodyCloneMesh);

        Buffer.Dispose(hairUVBuffer, faceUVBuffer, bodyUVBuffer);

        return true;
    }
    #endregion CombineMesh
    
    #region CombineMaps
    public void CombineMaps()
    {
        if (diffuseResult != null) SafeDestory(diffuseResult);
        if (essgMaskResult != null) SafeDestory(essgMaskResult);
        if (selfMaskResult != null) SafeDestory(selfMaskResult);
        if (outlineMaskResult != null) SafeDestory(outlineMaskResult);

        List<CombineTextureInfo> combineTextureInfoList = new List<CombineTextureInfo>();

        //Diffuse
        int textureSize = 1024;
        combineTextureInfoList.Add(new CombineTextureInfo(hair.GetTexture2D("_diffuse"), new Rect(0, 0, textureSize * 0.375f, textureSize * 0.75f)));
        combineTextureInfoList.Add(new CombineTextureInfo(face.GetTexture2D("_diffuse"), new Rect(0, textureSize * 0.75f, textureSize * 0.375f, textureSize * 0.25f)));
        combineTextureInfoList.Add(new CombineTextureInfo(body.GetTexture2D("_diffuse"),  new Rect(textureSize * 0.375f, 0, textureSize * 0.625f, textureSize)));
        Combine(combineTextureInfoList.ToArray(), textureSize, ref diffuseResult);
        diffuseResult.name = "_diffuse";
        combineTextureInfoList.Clear();

        //ESSGMask
        textureSize = 512;
        combineTextureInfoList.Add(new CombineTextureInfo(hair.GetTexture2D("_mask"),  new Rect(0, 0, textureSize * 0.375f, textureSize * 0.75f)));
        combineTextureInfoList.Add(new CombineTextureInfo(face.GetTexture2D("_mask"), new Rect(0, textureSize * 0.75f, textureSize * 0.375f, textureSize * 0.25f)));
        combineTextureInfoList.Add(new CombineTextureInfo(body.GetTexture2D("_mask"), new Rect(textureSize * 0.375f, 0, textureSize * 0.625f, textureSize)));
        Combine(combineTextureInfoList.ToArray(), textureSize, ref essgMaskResult);
        essgMaskResult.name = "_mask";
        combineTextureInfoList.Clear();

        //SelfMask
        textureSize = 512;
        combineTextureInfoList.Add(new CombineTextureInfo(hair.GetTexture2D("_SelfMask"), new Rect(0, 0, 0, 0),new Rect(0, 0, textureSize * 0.375f, textureSize * 0.75f)));
        combineTextureInfoList.Add(new CombineTextureInfo(face.GetTexture2D("_SelfMask"), new Rect(0, 0, textureSize, textureSize), new Rect(0, textureSize * 0.75f, textureSize * 0.375f, textureSize * 0.25f)));
        combineTextureInfoList.Add(new CombineTextureInfo(body.GetTexture2D("_SelfMask"), new Rect(0, 0, 0, 0), new Rect(textureSize * 0.375f, 0, textureSize * 0.625f, textureSize)));
        Combine(combineTextureInfoList.ToArray(), textureSize, ref selfMaskResult);
        selfMaskResult.name = "_SelfMask";
        combineTextureInfoList.Clear();


        //SelfMask
        textureSize = 128;
        combineTextureInfoList.Add(new CombineTextureInfo(hair.GetTexture2D("_OutlineWidthControl"), new Rect(0, 0, textureSize * 0.375f, textureSize * 0.75f)));
        combineTextureInfoList.Add(new CombineTextureInfo(face.GetTexture2D("_OutlineWidthControl"), new Rect(0, textureSize * 0.75f, textureSize * 0.375f, textureSize * 0.25f)));
        combineTextureInfoList.Add(new CombineTextureInfo(body.GetTexture2D("_OutlineWidthControl"), new Rect(textureSize * 0.375f, 0, textureSize * 0.625f, textureSize)));
        Combine(combineTextureInfoList.ToArray(), textureSize, ref outlineMaskResult);
        outlineMaskResult.name = "_OutlineWidthControl";
        combineTextureInfoList.Clear();


    }


    public bool Combine(CombineTextureInfo[] sourceTextures, int textureSize, ref RenderTexture combinedTexture)
    {
        combinedTexture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
        combinedTexture.enableRandomWrite = true;
        combinedTexture.wrapMode = TextureWrapMode.Repeat;
        combinedTexture.Create();
        RenderTexture.active = combinedTexture;
        GL.Clear(true, true, Color.clear);        

        int kernel = computeShader.FindKernel("CombineMaps");
        uint x, y, z;
        computeShader.GetKernelThreadGroupSizes(kernel, out x, out y, out z);

        for (int i = 1; i <= 10; i++)
        {           
            Matrix4x4 rect_RGBA = Matrix4x4.identity;
            var info = i <= sourceTextures.Length ? sourceTextures[i - 1] : null;
            var map = info != null ? info.texture : Texture2D.whiteTexture;            
            var texelSize = new float4(map.width, map.height, new float2(0));
            if (info != null)
            {
                for (int j = 0; j <= 3; j++)
                {
                    rect_RGBA.SetRow(j, new float4(info.uvRect[j].position, info.uvRect[j].size));
                }
            }
            computeShader.SetMatrix(string.Format("Map_{0}_Rect_RGBA", i.ToString("00")), rect_RGBA);            
            computeShader.SetTexture(kernel, string.Format("Map_{0}", i.ToString("00")), map);
            computeShader.SetVector(string.Format("Map_{0}_TexelSize", i.ToString("00")), texelSize);
        }

        computeShader.SetFloat("mapCount", sourceTextures.Length);
        computeShader.SetVector("_Combined_TexelSize", new float4(combinedTexture.width, combinedTexture.height, 0.0f, 0.0f));
        computeShader.SetTexture(kernel, "Combined", combinedTexture);
        computeShader.Dispatch(kernel, Mathf.CeilToInt(combinedTexture.width / x), Mathf.CeilToInt(combinedTexture.height / y), Mathf.CeilToInt(z));

        return combinedTexture != null;
    }
    #endregion CombineMaps

    #region CombineMaterials
    public bool CombineMaterials()
    {
        SafeDestory(result.material);
        if(result.material == null)
        {
            if (result.renderMode == CombineGroup.RenderMode.SkinnedMeshRenderer)
            {
                if (result.skinnedMeshRenderer.sharedMaterial == null)
                    result.skinnedMeshRenderer.sharedMaterial = Instantiate(body.material);
                result.material = result.skinnedMeshRenderer.sharedMaterial;
                
            }
            if (result.renderMode == CombineGroup.RenderMode.Meshrenderer)
            {
                if (result.meshRenderer.sharedMaterial == null)
                    result.meshRenderer.sharedMaterial = Instantiate(body.material);
                result.material = result.meshRenderer.sharedMaterial;
            }
        }
        result.material.SetFloat("_SelfMaskDirection", 1.0f);
        result.material.name = "Combine_Material";

        result.material.SetTexture("_diffuse", diffuseResult);
        result.material.SetTexture("_mask", essgMaskResult);
        result.material.SetTexture("_SelfMask", selfMaskResult);
        result.material.SetTexture("_OutlineWidthControl", outlineMaskResult);

        result.material.SetTexture("_ExpressionMap", face.material.GetTexture("_ExpressionMap"));
        result.material.SetTexture("_ExpressionQMap", face.material.GetTexture("_ExpressionQMap"));
        result.material.SetVector("_BrowRect", face.material.GetVector("_BrowRect"));
        result.material.SetVector("_FaceRect", face.material.GetVector("_FaceRect"));
        result.material.SetVector("_MouthRect", face.material.GetVector("_MouthRect"));

        return true;
    }
    #endregion CombineMaterials


}
#if UNITY_EDITOR
[CustomEditor(typeof(CharacterCombine))]
public class CharacterCombine_Editor:Editor
{
    CharacterCombine data;
    private void OnEnable()
    {
        data = target as CharacterCombine;
    }
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Update Groups"))
        {
            data.UpdateGroups();
        }
        if (GUILayout.Button("Combine"))
        {
            if (data.result.renderMode == CharacterCombine.CombineGroup.RenderMode.SkinnedMeshRenderer && data.result.skinnedMeshRenderer == null)
            {
                Debug.Log("You must be assign renderer.");
                return;
            }
            if (data.result.renderMode == CharacterCombine.CombineGroup.RenderMode.Meshrenderer && data.result.meshRenderer == null)
            {
                Debug.Log("You must be assign renderer.");
                return;
            }
            data.DoCombine();

            //Selection.activeGameObject = data.combineResult.gameObject;
        }
        if (GUILayout.Button("Save to Project"))
        {
            data.SaveMapsAndAssignToMaterial();
        }
    }
}
#endif
