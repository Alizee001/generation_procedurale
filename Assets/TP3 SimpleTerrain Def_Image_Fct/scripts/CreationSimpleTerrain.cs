using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using System.Collections;
using System.Runtime.CompilerServices;


#if UNITY_EDITOR
using UnityEditor;
#endif

// ================================================================
// TP5 -> TP4 : Jobs + Burst pour accelerer les traitements de generation
// ================================================================

[BurstCompile(CompileSynchronously = true)]
public struct SinusoideTerrainJob : IJobParallelFor
{
    public NativeArray<Vector3> vertices;
    public float frequence;
    public float hauteur;

    public void Execute(int index)
    {
        Vector3 v = vertices[index];
        v.y = Mathf.Sin(v.x * frequence * 2f)
            * Mathf.Sin(v.z * frequence * 2f)
            * hauteur;
        vertices[index] = v;
    }
}

[BurstCompile(CompileSynchronously = true)]
public struct CollineTerrainJob : IJobParallelFor
{
    public NativeArray<Vector3> vertices;
    public float centreX;
    public float centreZ;
    public float sigma;
    public float hauteur;

    public void Execute(int index)
    {
        Vector3 v = vertices[index];
        float dx = v.x - centreX;
        float dz = v.z - centreZ;
        float distanceCarree = dx * dx + dz * dz;
        float facteur = Mathf.Exp(-distanceCarree / (2f * sigma * sigma));
        v.y = hauteur * facteur;
        vertices[index] = v;
    }
}
public struct PerlinTerrainJob : IJobParallelFor
{
    public NativeArray<Vector3> vertices;
    public float echelle;
    public float seed;
    public float hauteur;

    public void Execute(int index)
    {
        Vector3 v = vertices[index];
        float x = v.x * echelle + seed;
        float z = v.z * echelle + seed;
        float bruit = Mathf.PerlinNoise(x, z);
        v.y = (bruit - 0.5f) * 2f * hauteur;
        vertices[index] = v;
    }
}

// Version Burst : bruit de Unity.Mathematics (autre bruit, valeurs ~[-1 ; 1]).
[BurstCompile(CompileSynchronously = true)]
public struct PerlinTerrainBurstJob : IJobParallelFor
{
    public NativeArray<Vector3> vertices;
    public float echelle;
    public float seed;
    public float hauteur;

    public void Execute(int index)
    {
        Vector3 v = vertices[index];
        Unity.Mathematics.float2 p = new Unity.Mathematics.float2(
            v.x * echelle + seed,
            v.z * echelle + seed);
        float bruit = Unity.Mathematics.noise.cnoise(p);
        v.y = bruit * hauteur;
        vertices[index] = v;
    }
}

[BurstCompile(CompileSynchronously = true)]
public struct TextureTerrainJob : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<Color> pixels;
    public NativeArray<Vector3> vertices;
    public int largeurImage;
    public int hauteurImage;
    public int resolution;
    public float hauteurMax;
    public void Execute(int index)
    {
        int i = index % resolution;
        int j = index / resolution;

        float u = (float)i / (resolution - 1);
        float v = (float)j / (resolution - 1);

        float px = u * (largeurImage - 1);
        float py = v * (hauteurImage - 1);

        int x0 = (int)px;
        int y0 = (int)py;
        int x1 = Mathf.Min(x0 + 1, largeurImage - 1);
        int y1 = Mathf.Min(y0 + 1, hauteurImage - 1);

        float tx = px - x0;
        float ty = py - y0;

        Color c00 = pixels[y0 * largeurImage + x0];
        Color c10 = pixels[y0 * largeurImage + x1];
        Color c01 = pixels[y1 * largeurImage + x0];
        Color c11 = pixels[y1 * largeurImage + x1];

        Color cx0 = Color.Lerp(c00, c10, tx);
        Color cx1 = Color.Lerp(c01, c11, tx);
        Color couleur = Color.Lerp(cx0, cx1, ty);

        Vector3 vertex = vertices[index];
        vertex.y = couleur.grayscale * hauteurMax;
        vertices[index] = vertex;
    }
}

[BurstCompile(CompileSynchronously = true)]
public struct NormalesVertexJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Vector3> vertices;
    [ReadOnly] public NativeArray<int> triangles;
    [ReadOnly] public NativeArray<int> debut;     
    [ReadOnly] public NativeArray<int> liste;     
    public int mode;                              
    [WriteOnly] public NativeArray<Vector3> normales;

    public void Execute(int index)
    {
        Vector3 somme = new Vector3(0f, 0f, 0f);

        for (int k = debut[index]; k < debut[index + 1]; k++)
        {
            int t = liste[k];
            int i0 = triangles[t];
            int i1 = triangles[t + 1];
            int i2 = triangles[t + 2];

            Vector3 v0 = vertices[i0];
            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];

            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0);
            float carre = n.sqrMagnitude;
            float longueur = Mathf.Sqrt(carre);
            Vector3 nUnitaire = carre < 0.000001f
                ? new Vector3(0f, 1f, 0f)
                : n / longueur;

            float poids = 1f;                               

            if (mode == 1)                                    
            {
                poids = longueur * 0.5f;
            }
            else if (mode == 2)                               
            {
                Vector3 a;
                Vector3 b;
                if (index == i0) { a = v1 - v0; b = v2 - v0; }
                else if (index == i1) { a = v0 - v1; b = v2 - v1; }
                else { a = v0 - v2; b = v1 - v2; }

                poids = (a.sqrMagnitude < 0.000001f || b.sqrMagnitude < 0.000001f)
                    ? 0f
                    : Vector3.Angle(a, b);
            }

            somme += nUnitaire * poids;
        }

        if (somme.sqrMagnitude < 0.000001f)
            somme = new Vector3(0f, 1f, 0f);
        else
            somme = somme.normalized;

        normales[index] = somme;
    }
}

// ================================================================
// TP5 : jobs complementaires
// ================================================================

// "Hello World" du TP (section 5) : remise a plat y = constante.
[BurstCompile(CompileSynchronously = true)]
public struct ReinitialiserTerrainJob : IJobParallelFor
{
    public NativeArray<Vector3> vertices_job;
    public float hauteurCst_job;

    public void Execute(int index)
    {
        Vector3 vertex = vertices_job[index];
        vertex.y = hauteurCst_job;
        vertices_job[index] = vertex;
    }
}

[BurstCompile(CompileSynchronously = true)]
public struct GrilleSyntheseJob : IJobParallelFor
{
    public NativeArray<Vector3> vertices;
    public int resolution;
    public float pas;

    public void Execute(int index)
    {
        vertices[index] = new Vector3((index / resolution) * pas,
                                      0f,
                                      (index % resolution) * pas);
    }
}

[BurstCompile(CompileSynchronously = true)]
public struct VoisinsCsrJob : IJob
{
    [ReadOnly] public NativeArray<int> triangles;
    public int nbVertices;
    public NativeArray<int> debut;    // taille nbVertices + 1, a zero au depart
    public NativeArray<int> liste;    // taille triangles.Length

    public void Execute()
    {
        // 1) nombre de triangles par vertex
        for (int i = 0; i < triangles.Length; i++)
            debut[triangles[i] + 1]++;

        // 2) cumul -> debut de la plage de chaque vertex
        for (int v = 0; v < nbVertices; v++)
            debut[v + 1] += debut[v];

        // 3) remplissage
        NativeArray<int> curseur = new NativeArray<int>(nbVertices, Allocator.Temp);
        for (int i = 0; i < triangles.Length; i += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                int v = triangles[i + k];
                liste[debut[v] + curseur[v]] = i;
                curseur[v]++;
            }
        }
        curseur.Dispose();
    }
}

// Sonde : indique si le code d'un job est bien compile par Burst.
// [BurstDiscard] : l'appel est supprime par Burst, donc "manage" reste false
// seulement quand Burst est actif.
[BurstCompile(CompileSynchronously = true)]
public struct SondeBurstJob : IJob
{
    public NativeArray<int> resultat;   // [0] = 1 si Burst, 0 si C# manage

    public void Execute()
    {
        bool manage = false;
        MarquerManage(ref manage);
        resultat[0] = manage ? 0 : 1;
    }

    [BurstDiscard]
    private static void MarquerManage(ref bool manage)
    {
        manage = true;
    }
}

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class CreationSimpleTerrain : MonoBehaviour
{
    [Header("Parametres du terrain")]

    [Range(1, 2000)]
    public float dimension = 100;

    [Range(1, 12)]
    [Tooltip("La resolution vaut 2^n")]
    public int puissance2Resolution = 4;

    public bool CentrerPivot = true;

    [HideInInspector]
    public ushort resolution;


    public enum ChoixModeDeformation
    {
        Fonction,
        Texture
    }

    [Header("Mode de generation")]
    public ChoixModeDeformation choixModeDeformation;


    public enum TypeFonction
    {
        Sinusoide,
        Collines,
        Perlin
    }

    public TypeFonction typeFonction;

    // --------------------------------------------------------------------
    // EXERCICE 2 : METRIQUES DE DISTANCE
    // --------------------------------------------------------------------
    public enum TypeDistance
    {
        Euclidienne,
        EuclidienneCarree,
        Manhattan,
        Chebyshev
    }

    [Header("Exercice 2 - Parametre de distance")]
    public TypeDistance distanceUtilisee = TypeDistance.Euclidienne;

    [Header("HeightMaps")]

    public int numTexture;

    public List<Texture2D> textures;

    [Header("Parametres des fonctions")]

    public float hauteurSinusoide = 5f;

    public float hauteurColline = 10f;

    public float largeurColline = 15f;

    public float hauteurPerlin = 10f;

    public float echellePerlin = 0.05f;

    public float hauteurTexture = 20f;

    [Header("TP5 - Jobs / Burst")]
    [Tooltip("Normales calculees par job (sinon ancienne boucle C#)")]
    public bool utiliserJobNormales = true;
    [Tooltip("Perlin : false = Mathf.PerlinNoise (Jobs seul), true = cnoise (Burst)")]
    public bool perlinBurst = false;

    // Creation des mesh et vertices pour le LOD
    private Mesh meshLOD0, meshLOD1, meshLOD2;

    //private Vector3[] vertices0, vertices1, vertices2;

    //private Vector3[] normales0, normales1, normales2;

    // Matrice de correspondance (Indice LOD1 -> Indice LOD0)
    //private int[] mapLOD1to0;

    //private int[] mapLOD2to0;

    //[Header("Normales")]
    public enum ModeNormale
    {
        Basique,
        Surface,
        Angle
    }

    public ModeNormale modeNormale = ModeNormale.Basique;

    [Header("Camera")]

    public float vitesseCamera = 15f;

    public float vitesseRotationCamera = 80f;

    public float vitesseRotationTerrain = 50f;

    // --- NOUVEAUX PARAMETRES POUR L'EXERCICE 1 ---
    [Header("Sculpture Interactive (Exercice 1)")]
    [Tooltip("Ajoutez des courbes allant de X=0 a X=1, et Y=0 a Y=1")]
    public AnimationCurve[] patternsDeformation;
    public float rayonDeformation = 15f;
    public float intensiteMaxDeformation = 10f;
    private int p_indexPatternCourant = 0;

    // --------------------------------------------------------------------
    // Donnees internes
    // --------------------------------------------------------------------
    private uint p_dimVertices;
    private uint p_dimTriangles;

    //private MeshCollider p_meshCollider;
    //private MeshFilter p_meshFilter;
    //private Mesh p_mesh;

    //private Vector3[] p_vertices;
    //private Vector3[] p_normals;
    //private Vector2[] p_uv;
    //private int[] p_triangles;

    private Camera p_cam;

    private LayerMask maskPickingTerrain;

    private float p_dimInterVertices;

    // Liste des triangles attachés à chaque vertex.
    //private List<int>[] p_trianglesParVertex;

    // Voisinage aplati en memoire native (format CSR) pour le job des normales.
    private NativeArray<int> p_trianglesNative;
    private NativeArray<int> p_debutVoisinsNative;
    private NativeArray<int> p_listeVoisinsNative;
    private bool p_voisinsNativesPrets = false;

    // Pour le picking des collines.
    private Vector3 p_dernierPointPicking;

    private bool p_pointPickingDisponible = false;

    // F10
    private int p_modeAffichageNormales = 0;
    private float p_tempsAffichageNormales = 0f;
    private const float DUREE_AFFICHAGE_NORMALES = 3f;

    // F11
    private float p_dernierAppuiF11 = -10f;
    private const float DELAI_DOUBLE_F11 = 0.5f;

    // F1
    private bool p_afficherAide = false;

    // Pour le Perlin.
    private float p_seedPerlin;

    // Rotation terrain.
    private bool p_rotationTerrain = false;

    // EXERCICE 2 : affichage des distances
    private bool p_afficherDistances = false;
    private Vector3 p_centreDistanceMonde;
    private float p_distanceMaxAffichee = 0f;
    private int p_nombreVoisins = 0;

    // TP5 : traitement asynchrone (F4)
    private bool p_asyncEnCours = false;
    private JobHandle p_handleAsync;
    private NativeArray<Vector3> p_asyncVertices;
    private NativeArray<Vector3> p_asyncNormales;
    private NativeArray<Color> p_asyncPixels;
    private float p_asyncDebut;

    // TP5 : FPS lisses
    private float p_fps;
    // Partie Chunk
    private class Chunk
    {
        public Vector2Int coord;
        public GameObject go;
        public MeshCollider collider;
        public int res;
        public Mesh mesh;                       
        public Vector3[] vertices, normals;
        public Vector2[] uv;
        public int[] triangles;
        public List<int>[] trianglesParVertex;
        public Mesh mesh1, mesh2;
        public Vector3[] vertices1, vertices2, normals1, normals2;
        public int[] map1, map2;
    }
    
    Dictionary<Vector2Int, Chunk> p_chunks = new Dictionary<Vector2Int, Chunk>();
    Vector2Int gridMin =Vector2Int.zero;
    Vector2Int gridMax =Vector2Int.zero;
    private Material p_materialBase;
    private readonly List<Chunk> p_membresTmp = new List<Chunk>();
    private readonly List<int> p_indicesTmp = new List<int>();

    private Chunk chunkBase => p_chunks.TryGetValue(Vector2Int.zero, out Chunk c) ? c : null;
    private Vector3[] p_vertices => chunkBase?.vertices;
    private Vector3[] p_normals => chunkBase?.normals;
    private Vector2[] p_uv => chunkBase?.uv;
    private int[] p_triangles => chunkBase?.triangles;
    private List<int>[] p_trianglesParVertex => chunkBase?.trianglesParVertex;


    private bool p_surbrillance = false;

    // --------------------------------------------------------------------
    // RESET
    // --------------------------------------------------------------------

    void Reset()
    {
        //p_meshFilter = GetComponent<MeshFilter>();
        //p_meshCollider = GetComponent<MeshCollider>();

        //if (GetComponent<MeshRenderer>() == null)
        //    gameObject.AddComponent<MeshRenderer>();

        //if (p_meshCollider == null)
        //    p_meshCollider = gameObject.AddComponent<MeshCollider>();

        gameObject.layer = LayerMask.NameToLayer("L_PickingTerrain");
    }

    // --------------------------------------------------------------------
    // AWAKE
    // --------------------------------------------------------------------
    void Awake()
    {
        p_cam = Camera.main;

        if (p_cam == null)
            p_cam = FindFirstObjectByType<Camera>();

        int layerTerrain = LayerMask.NameToLayer("L_PickingTerrain");

        if (layerTerrain >= 0)
        {
            gameObject.layer = layerTerrain;
            maskPickingTerrain = LayerMask.GetMask("L_PickingTerrain");
        }
        else
        {
            maskPickingTerrain = ~0;
        }

        p_seedPerlin = Random.Range(0f, 10000f);

        MeshRenderer mr = GetComponent<MeshRenderer>();
        p_materialBase = mr.sharedMaterial;
        mr.enabled = false;
        GetComponent<MeshCollider>().enabled = false;
    }
    // --------------------------------------------------------------------
    // START
    // --------------------------------------------------------------------

    void Start()
    {
        creerChunk(Vector2Int.zero);

        resolution = (ushort)(1 << puissance2Resolution);

        //if (puissance2Resolution >= 6)
        //{
        //    initialiserLODGroup();
        //}
        //else
        //{
        //    creerLeMeshTerrain();
        //}

    }
    // --------------------------------------------------------------------
    // LIBERATION DE LA MEMOIRE NATIVE
    // --------------------------------------------------------------------

    void OnDestroy()
    {
        p_handleAsync.Complete();
        libererAsync();
        libererVoisinsNatifs();
    }

    // --------------------------------------------------------------------
    // INITIALISATION DU GROUPE LOD
    // --------------------------------------------------------------------
    //void initialiserLODGroup()
    //{
    //    int resLOD0 = 1 << puissance2Resolution;
    //    int resLOD2 = 16;
    //    int resLOD1 = 1 << (4 + (puissance2Resolution - 4) / 2);

    //    // Création de la hiérarchie LODGroup
    //    LODGroup lodGroup = gameObject.AddComponent<LODGroup>();
    //    LOD[] lods = new LOD[3];

    //    p_meshFilter = GetComponent<MeshFilter>();
    //    p_meshCollider = GetComponent<MeshCollider>();

    //    // Création des 3 enfants MeshRenderer
    //    Renderer[] renderer0 = new Renderer[] { creerEnfantsLOD("LOD_0", resLOD0, out meshLOD0, out vertices0, out normales0, out p_uv, out p_triangles) };
    //    Renderer[] renderer1 = new Renderer[] { creerEnfantsLOD("LOD_1", resLOD1, out meshLOD1, out vertices1, out normales1, out _, out _) };
    //    Renderer[] renderer2 = new Renderer[] { creerEnfantsLOD("LOD_2", resLOD2, out meshLOD2, out vertices2, out normales2, out _, out _) };

    //    // Références vers LOD0 pour le picking et le calcul
    //    p_mesh = meshLOD0;
    //    p_vertices = vertices0;
    //    p_normals = normales0;
    //    resolution = (ushort)resLOD0;

    //    p_meshCollider.sharedMesh = p_mesh;
    //    bakerVoisins();

    //    // Ajustement des seuils de basculementde de distance
    //    lods[0] = new LOD(0.5f, renderer0);
    //    lods[1] = new LOD(0.15f, renderer1);
    //    lods[2] = new LOD(0.02f, renderer2);

    //    lodGroup.SetLODs(lods);
    //    lodGroup.RecalculateBounds();

    //    calculerMapping(resLOD0, resLOD1, out mapLOD1to0);
    //    calculerMapping(resLOD0, resLOD2, out mapLOD2to0);

    //}

    // --------------------------------------------------------------------
    // CREATION DES ENFANTS DES LOD
    // --------------------------------------------------------------------
    Renderer creerEnfantsLOD(Transform parent, string nom, int res, out Mesh mesh, out Vector3[] vertices, out Vector3[] norms, out Vector2[] uvs, out int[] tris)
    {
        GameObject child = new GameObject(nom);
        child.transform.SetParent(parent, false);

        MeshFilter mf = child.AddComponent<MeshFilter>();
        MeshRenderer mr = child.AddComponent<MeshRenderer>();
        //mr.sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;

        mr.sharedMaterial = p_materialBase;

        //MeshRenderer mainRenderer = GetComponent<MeshRenderer>();
        //if (mainRenderer != null)
        //    mr.sharedMaterial = mainRenderer.sharedMaterial;

        // Generer inline du maillage selon la resolution passe en parametre
        mesh = creerMeshGrille(res, out vertices, out norms, out uvs, out tris);
        mf.sharedMesh = mesh;
        return mr;
    }

    private List<Chunk> chunkTouches (Vector3 pointMonde)
    {
        List<Chunk> chunks = new List<Chunk>();
        float origine = CentrerPivot ? dimension * 0.5f : 0f;
        foreach (Chunk chunk in p_chunks.Values)
        {
            float marg = rayonDeformation + 2f * dimension / (chunk.res - 1);
            Vector3 localPoint = chunk.go.transform.InverseTransformPoint(pointMonde);
            if (localPoint.x >= -origine - marg && localPoint.x <= dimension - origine +marg &&
                localPoint.z >= -origine - marg && localPoint.z <= dimension - origine +marg)
            {
                chunks.Add(chunk);
            }
        }
        return chunks;
    }

    private Chunk creerChunk(Vector2Int coord)
    {
        Chunk chunk = new Chunk();
        chunk.coord = coord;
        chunk.res = 1 << puissance2Resolution;

        chunk.go = new GameObject("Chunk_" + coord.x + "_" + coord.y);
        chunk.go.transform.SetParent(transform, false);
        chunk.go.transform.localPosition = new Vector3(coord.x * dimension, 0f, coord.y * dimension);
        chunk.go.layer = gameObject.layer;
        chunk.collider = chunk.go.AddComponent<MeshCollider>();

        if (puissance2Resolution >= 6)
        {
            creerLODsChunk(chunk);
        }
        else
        {
            chunk.mesh = creerMeshGrille(chunk.res, out chunk.vertices, out chunk.normals, out chunk.uv, out chunk.triangles);
            chunk.go.AddComponent<MeshFilter>().sharedMesh = chunk.mesh;
            chunk.go.AddComponent<MeshRenderer>().sharedMaterial = p_materialBase;
        }

        bakerVoisins(chunk);
        p_chunks[coord] = chunk;

        // Les vertices frontières reprennent position ET normale de leurs jumeaux existants
        int last = chunk.res - 1;
        for (int i =0; i < chunk.res; i++)
        {
            copierDepuisJumeaux(chunk, i, 0);
            copierDepuisJumeaux(chunk, i, last);
            copierDepuisJumeaux(chunk, 0, i);
            copierDepuisJumeaux(chunk, last, i);
        }

        appliquerMeshChunk(chunk);
        return chunk;
    }

    private void copierDepuisJumeaux(Chunk chunk, int index_x, int index_z)
    {
        trouverJumeaux(chunk, index_x, index_z);
        if (p_membresTmp.Count == 0)
            return;
        int index = index_z * chunk.res + index_x;
        chunk.vertices[index].y = p_membresTmp[0].vertices[p_indicesTmp[0]].y;
        chunk.normals[index] = p_membresTmp[0].normals[p_indicesTmp[0]];
    }

    private void creerLODsChunk(Chunk chunk)
    {
        int resLOD1 = 1 << (4 + (puissance2Resolution - 4) / 2);
        int resLOD2 = 16;

        LODGroup lodGroup = chunk.go.AddComponent<LODGroup>();
        Renderer r0 = creerEnfantsLOD(chunk.go.transform, "LOD_0", chunk.res, out chunk.mesh, out chunk.vertices, out chunk.normals, out chunk.uv, out chunk.triangles);
        Renderer r1 = creerEnfantsLOD(chunk.go.transform, "LOD_1", resLOD1, out chunk.mesh1, out chunk.vertices1, out chunk.normals1, out _, out _);
        Renderer r2 = creerEnfantsLOD(chunk.go.transform, "LOD_2", resLOD2, out chunk.mesh2, out chunk.vertices2, out chunk.normals2, out _, out _);

        lodGroup.SetLODs(new LOD[] {
            new LOD(0.5f, new Renderer[] { r0 }),
            new LOD(0.15f, new Renderer[] { r1 }),
            new LOD(0.02f, new Renderer[] { r2 })
        });
        lodGroup.RecalculateBounds();

        calculerMapping(chunk.res, resLOD1, out chunk.map1);
        calculerMapping(chunk.res, resLOD2, out chunk.map2);

    }

    private void trouverJumeaux(Chunk chunk, int index_x, int index_z)
    {
        p_membresTmp.Clear();
        p_indicesTmp.Clear();
        int last = chunk.res - 1;

        int dxMin = (index_x == 0) ? -1 : 0;
        int dxMax = (index_x == last) ? 1 : 0;
        int dzMin = (index_z == 0) ? -1 : 0;
        int dzMax = (index_z == last) ? 1 : 0;

        for (int dz = dzMin; dz <= dzMax; dz++)
        {
            for (int dx = dxMin; dx <= dxMax; dx++)
            {
                if (dx == 0 && dz == 0)
                    continue;
                if (!p_chunks.TryGetValue(new Vector2Int(chunk.coord.x + dx, chunk.coord.y + dz), out Chunk voisin))
                    continue;
                 int voisinx = (dx == -1) ? last : (dx == 1) ? 0 : index_x;
                 int voisinz = (dz == -1) ? last : (dz == 1) ? 0 : index_z;

                p_membresTmp.Add(voisin);
                p_indicesTmp.Add(voisinx * voisin.res + voisinz);
            }
        }
    }

    private Vector3 sommeNormales(Chunk chunk, int vertex)
    {
        Vector3 somme = Vector3.zero;

        foreach (int triangleIndex in chunk.trianglesParVertex[vertex])
        {
            Vector3 normale = calculerNormaleTriangle(chunk, triangleIndex);
            if (modeNormale == ModeNormale.Surface)
                normale *= calculerSurfaceTriangle(chunk, triangleIndex);
            else if (modeNormale == ModeNormale.Angle)
                normale *= calculerAngleAuVertex(chunk, triangleIndex, vertex);
            somme += normale;
        }
        return somme;
    }

    private void recalculerNormalesChunk(Chunk chunk)
    {
        int last = chunk.res - 1;
        for (int index_z = 0; index_z < chunk.res; index_z++)
        {
            for (int index_x = 0; index_x < chunk.res; index_x++)
            {
                int i = index_z * chunk.res + index_x;
                Vector3 somme = sommeNormales(chunk, i);
                bool bord = (index_x == 0 || index_x == last || index_z == 0 || index_z == last);
                if (bord)
                {
                    trouverJumeaux(chunk, index_x, index_z);
                    for (int k = 0; k < p_membresTmp.Count; k++)
                    {
                        Chunk voisin = p_membresTmp[k];
                        int indexVoisin = p_indicesTmp[k];
                        somme += sommeNormales(voisin, indexVoisin);
                    }
                    
                }
                Vector3 normale = somme.sqrMagnitude < 0.000001f ? Vector3.up : somme.normalized;
                chunk.normals[i] = normale;

                if (bord)
                {
                    for (int k = 0; k < p_membresTmp.Count; k++)
                    {
                        Chunk voisin = p_membresTmp[k];
                        int indexVoisin = p_indicesTmp[k];
                        voisin.normals[indexVoisin] = normale;
                    }
                }
            }
        }
    }

    // --------------------------------------------------------------------
    // CALCUL DU MAPPING
    // --------------------------------------------------------------------
    void calculerMapping(int resHaut, int resBasse, out int[] map)
    {
        map = new int[resBasse * resBasse];

        int idx = 0;
        for (int z = 0; z < resBasse; z++)
        {
            for (int x = 0; x < resBasse; x++)
            {
                int xHaut = Mathf.RoundToInt(x * (resHaut - 1) / (float)(resBasse - 1));
                int zHaut = Mathf.RoundToInt(z * (resHaut - 1) / (float)(resBasse - 1));
                map[idx++] = zHaut * resHaut + xHaut;

            }
        }

    }

    // --------------------------------------------------------------------
    // SYNCHRONISATION DES MODIFS DE HAUTEUR ET DE NORMAL
    // --------------------------------------------------------------------
    private void propagerLOD(Chunk chunk)
    {
        if (puissance2Resolution < 6)
            return;

        // Mise à jour du LOD1
        if (chunk.mesh1 != null)
            propagerVers(chunk, chunk.mesh1, chunk.vertices1, chunk.normals1, chunk.map1);

        // Mise à jour du LOD2
        if (chunk.mesh2 != null)
            propagerVers(chunk, chunk.mesh2, chunk.vertices2, chunk.normals2, chunk.map2);


        //if(meshLOD1 != null && mapLOD1to0 != null)
        //{
        //    for (int i = 0; i < vertices1.Length; i++)
        //    {
        //        int i0 = mapLOD1to0[i];
        //        vertices1[i].y = vertices0[i0].y;
        //    }
        //    meshLOD1.vertices = vertices1;
        //    meshLOD1.RecalculateNormals();
        //}
        //// Mise à jour du LOD2
        //if(meshLOD2 != null && mapLOD2to0 != null)
        //{
        //    for (int i = 0; i < vertices2.Length; i++)
        //    {
        //        int i0 = mapLOD2to0[i];
        //        vertices2[i].y = vertices0[i0].y;
        //    }
        //    meshLOD2.vertices = vertices2;
        //    meshLOD2.RecalculateNormals();
        //}
    }

    private void propagerVers(Chunk chunk, Mesh mesh, Vector3[] vertices, Vector3[] normals, int[] map)
    {
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].y = chunk.vertices[map[i]].y;
            normals[i] = chunk.normals[map[i]];
        }
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.RecalculateBounds();

    }


    private void etendreTerrain(Vector2Int direction)
    {
        if (p_chunks.Count == 0)
        return;

        if (direction.x != 0)
        {
            int x = (direction.x > 0) ? gridMax.x + 1 : gridMin.x - 1;
            for (int z = gridMin.y; z <= gridMax.y; z++)
            {
                creerChunk(new Vector2Int(x, z));
            }
            if (direction.x > 0)
                gridMax.x = x;
            else
                gridMin.x = x;

        }
        else
        {
            int z = (direction.y > 0) ? gridMax.y + 1 : gridMin.y - 1;
            for (int x = gridMin.x; x <= gridMax.x; x++)
            {
                creerChunk(new Vector2Int(x, z));
            }
            if (direction.y > 0)
                gridMax.y = z;
            else
                gridMin.y = z;
        }

            
    }

    private void appliquerMeshChunk(Chunk chunk)
    {
        if (chunk.mesh != null)
        {
            chunk.mesh.vertices = chunk.vertices;
            chunk.mesh.normals = chunk.normals;
            chunk.mesh.uv = chunk.uv;
            chunk.mesh.triangles = chunk.triangles;
            chunk.mesh.RecalculateBounds();
        }
        if (chunk.collider != null)
        {
            chunk.collider.sharedMesh = null;
            chunk.collider.sharedMesh = chunk.mesh;
        }
        propagerLOD(chunk);
    }

    // --------------------------------------------------------------------
    // CREATION DU MAILLAGE
    // --------------------------------------------------------------------

    //private void creerLeMeshTerrain(Chunk chunk)
    //{
    //    p_meshFilter = GetComponent<MeshFilter>();
    //    p_meshCollider = GetComponent<MeshCollider>();

    //    if (p_meshFilter == null)
    //        p_meshFilter = gameObject.AddComponent<MeshFilter>();

    //    if (p_meshCollider == null)
    //        p_meshCollider = gameObject.AddComponent<MeshCollider>();


    //    // ------------------------------------------------------------
    //    // Résolution
    //    // ------------------------------------------------------------

    //    int resolutionInt = 1 << puissance2Resolution;

    //    resolution = (ushort)Mathf.Clamp(
    //        resolutionInt,
    //        2,
    //        ushort.MaxValue
    //    );

    //    resolutionInt = resolution;


    //    // ------------------------------------------------------------
    //    // Nombre de vertices
    //    // ------------------------------------------------------------

    //    long nombreVertices = (long)resolutionInt * resolutionInt;

    //    long nombreTriangles =
    //        2L *
    //        (resolutionInt - 1) *
    //        (resolutionInt - 1);


    //    if (nombreVertices > int.MaxValue)
    //    {
    //        Debug.LogError("Le maillage contient trop de vertices.");
    //        return;
    //    }


    //    p_dimVertices = (uint)nombreVertices;
    //    p_dimTriangles = (uint)nombreTriangles;


    //    // ------------------------------------------------------------
    //    // Espacement
    //    // ------------------------------------------------------------

    //    p_dimInterVertices =
    //        dimension / (resolutionInt - 1);


    //    // ------------------------------------------------------------
    //    // Création des tableaux
    //    // ------------------------------------------------------------

    //    //p_vertices = new Vector3[nombreVertices];
    //    //p_normals = new Vector3[nombreVertices];
    //    //p_uv = new Vector2[nombreVertices];

    //    //p_triangles = new int[nombreTriangles * 3];


    //    // ------------------------------------------------------------
    //    // Création des vertices
    //    // ------------------------------------------------------------

    //    //float origine = CentrerPivot
    //    //    ? dimension * 0.5f
    //    //    : 0f;


    //    //for (int z = 0; z < resolutionInt; z++)
    //    //{
    //    //    for (int x = 0; x < resolutionInt; x++)
    //    //    {
    //    //        int index = z * resolutionInt + x;

    //    //        float px = x * p_dimInterVertices - origine;
    //    //        float pz = z * p_dimInterVertices - origine;

    //    //        p_vertices[index] =
    //    //            new Vector3(px, 0f, pz);


    //    //        // UV entre 0 et 1.
    //    //        float u =
    //    //            (float)x / (resolutionInt - 1);

    //    //        float v =
    //    //            (float)z / (resolutionInt - 1);

    //    //        p_uv[index] =
    //    //            new Vector2(u, v);

    //    //        p_normals[index] =
    //    //            Vector3.up;
    //    //    }
    //    //}


    //    // ------------------------------------------------------------
    //    // Création des triangles
    //    //
    //    // 3 -- 2
    //    // |  / |
    //    // 0 -- 1
    //    //
    //    // ------------------------------------------------------------

    //    //int triangleIndex = 0;

    //    //for (int z = 0; z < resolutionInt - 1; z++)
    //    //{
    //    //    for (int x = 0; x < resolutionInt - 1; x++)
    //    //    {
    //    //        int v0 = z * resolutionInt + x;
    //    //        int v1 = v0 + 1;
    //    //        int v2 = v0 + resolutionInt;
    //    //        int v3 = v2 + 1;


    //    //        // Triangle 1
    //    //        p_triangles[triangleIndex++] = v0;
    //    //        p_triangles[triangleIndex++] = v2;
    //    //        p_triangles[triangleIndex++] = v1;


    //    //        // Triangle 2
    //    //        p_triangles[triangleIndex++] = v1;
    //    //        p_triangles[triangleIndex++] = v2;
    //    //        p_triangles[triangleIndex++] = v3;
    //    //    }
    //    //}


    //    // ------------------------------------------------------------
    //    // Création du Mesh
    //    // ------------------------------------------------------------

    //    //if (p_mesh != null)
    //    //{
    //    //    Destroy(p_mesh);
    //    //}

    //    //p_mesh = new Mesh();

    //    //p_mesh.name = "TerrainProcedural";


    //    // ------------------------------------------------------------
    //    // IMPORTANT :
    //    // Plus de 65535 vertices => indices 32 bits.
    //    // ------------------------------------------------------------

    //    //if (nombreVertices > 65535)
    //    //{
    //    //    p_mesh.indexFormat = IndexFormat.UInt32;
    //    //}
    //    //else
    //    //{
    //    //    p_mesh.indexFormat = IndexFormat.UInt16;
    //    //}


    //    //p_mesh.vertices = p_vertices;
    //    //p_mesh.uv = p_uv;
    //    //p_mesh.triangles = p_triangles;
    //    //p_mesh.normals = p_normals;

    //    //p_mesh.RecalculateBounds();

    //    p_mesh = creerMeshGrille(resolutionInt, out p_vertices, out p_normals, out p_uv, out p_triangles);

    //    p_meshFilter.sharedMesh = p_mesh;

    //    p_meshCollider.sharedMesh = null;
    //    p_meshCollider.sharedMesh = p_mesh;


    //    // ------------------------------------------------------------
    //    // Baking des voisins
    //    // ------------------------------------------------------------

    //    bakerVoisins();


    //    Debug.Log(
    //        "Terrain créé : " +
    //        p_dimVertices +
    //        " vertices, " +
    //        p_dimTriangles +
    //        " triangles."
    //    );
    //}

    private IEnumerator surligneChunks()
    {
        p_surbrillance = true;
        List<Material> temp = new List<Material>();
        int n = 0;

        foreach (Chunk chunk in p_chunks.Values)
        {
            Material mats = new Material(p_materialBase);
            mats.color = Color.HSVToRGB((n++ * 0.17f)%1f, 0.7f, 1f);
            temp.Add(mats);
            foreach (MeshRenderer mr in chunk.go.GetComponentsInChildren<MeshRenderer>())
            {
                mr.material = mats;
            }
        }

        yield return new WaitForSeconds(3f);

        foreach (Chunk chunk in p_chunks.Values)
        {
            foreach (MeshRenderer mr in chunk.go.GetComponentsInChildren<MeshRenderer>())
            {
                mr.material = p_materialBase;
            }
        }

        foreach (Material mats in temp)
        {
            Destroy(mats);
        }

        p_surbrillance = false;
    }

    private Mesh creerMeshGrille(int res, out Vector3[] vertices, out Vector3[] norms, out Vector2[] uvs, out int[] tris)
    {
        vertices = new Vector3[res * res];
        norms = new Vector3[res * res];
        uvs = new Vector2[res * res];
        tris = new int[(res - 1) * (res - 1) * 6];

        float step = dimension / (res - 1);
        float orig;
        if (CentrerPivot)
            orig = dimension * 0.5f;
        else
            orig = 0f;

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                int idx = z * res + x;
                vertices[idx] = new Vector3(x * step - orig, 0f, z * step - orig);
                uvs[idx] = new Vector2((float)x / (res - 1), (float)z / (res - 1));
                norms[idx] = Vector3.up;

            }
        }

        int tIdx = 0;
        for (int z = 0; z < res - 1; z++)
        {
            for (int x = 0; x < res - 1; x++)
            {
                int v0 = z * res + x;
                int v1 = v0 + 1;
                int v2 = v0 + res;
                int v3 = v2 + 1;

                tris[tIdx++] = v0;
                tris[tIdx++] = v2;
                tris[tIdx++] = v1;

                tris[tIdx++] = v1;
                tris[tIdx++] = v2;
                tris[tIdx++] = v3;
            }
        }
        Mesh mesh = new Mesh();
        mesh.name = "Mesh_Res" + res;
        if (vertices.Length > 65535)
            mesh.indexFormat = IndexFormat.UInt32;

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.normals = norms;
        mesh.RecalculateBounds();

        return mesh;
    }


    // --------------------------------------------------------------------
    // BAKING DES VOISINS
    // --------------------------------------------------------------------

    private void bakerVoisins(Chunk chunk)
    {
        chunk.trianglesParVertex =
            new List<int>[chunk.vertices.Length];

        if (chunk.triangles == null || chunk.vertices == null)
            return;

        for (int i = 0; i < chunk.trianglesParVertex.Length; i++)
        {
            chunk.trianglesParVertex[i] =
                new List<int>();
        }


        // Chaque triangle est associé à ses 3 vertices.
        for (int i = 0; i < chunk.triangles.Length; i += 3)
        {
            int a = chunk.triangles[i];
            int b = chunk.triangles[i + 1];
            int c = chunk.triangles[i + 2];

            chunk.trianglesParVertex[a].Add(i);
            chunk.trianglesParVertex[b].Add(i);
            chunk.trianglesParVertex[c].Add(i);
        }

        // TP5 : version aplatie (natif) pour le job des normales.
        bakerVoisinsNatif();
    }

    // TP6 (plus-value) : construction du voisinage par un IJob Burst lance avec Run().
    private void bakerVoisinsNatif()
    {
        libererVoisinsNatifs();

        int nbV = p_vertices.Length;

        p_trianglesNative = new NativeArray<int>(p_triangles, Allocator.Persistent);
        p_debutVoisinsNative = new NativeArray<int>(nbV + 1, Allocator.Persistent);       // zeros
        p_listeVoisinsNative = new NativeArray<int>(p_triangles.Length, Allocator.Persistent);

        VoisinsCsrJob job = new VoisinsCsrJob
        {
            triangles = p_trianglesNative,
            nbVertices = nbV,
            debut = p_debutVoisinsNative,
            liste = p_listeVoisinsNative
        };
        job.Run();

        p_voisinsNativesPrets = true;
    }

    // Version C# manage d'origine, conservee uniquement pour la comparaison (F5).
    private void construireVoisinsCSharp(int nbV, out int[] debut, out int[] liste)
    {
        debut = new int[nbV + 1];
        liste = new int[p_triangles.Length];

        for (int i = 0; i < p_triangles.Length; i++)
            debut[p_triangles[i] + 1]++;

        for (int v = 0; v < nbV; v++)
            debut[v + 1] += debut[v];

        int[] curseur = new int[nbV];
        for (int i = 0; i < p_triangles.Length; i += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                int v = p_triangles[i + k];
                liste[debut[v] + curseur[v]] = i;
                curseur[v]++;
            }
        }
    }

    private void libererVoisinsNatifs()
    {
        if (p_trianglesNative.IsCreated) p_trianglesNative.Dispose();
        if (p_debutVoisinsNative.IsCreated) p_debutVoisinsNative.Dispose();
        if (p_listeVoisinsNative.IsCreated) p_listeVoisinsNative.Dispose();
        p_voisinsNativesPrets = false;
    }


    // --------------------------------------------------------------------
    // TERRAIN PLAT
    // --------------------------------------------------------------------

    private void remettreTerrainPlat()
    {
        foreach (var chunk in p_chunks.Values)
        {
            for (int i = 0; i < chunk.vertices.Length; i++)
            {
                chunk.vertices[i].y = 0f;
            }
            
        }
        
        recalculerToutesLesNormales();
        appliquerMesh();
    }

    // --------------------------------------------------------------------
    // APPLICATION DU MESH
    // --------------------------------------------------------------------

    private void appliquerMesh()
    {
        foreach (var chunk in p_chunks.Values)
        {
            appliquerMeshChunk(chunk);
        }
        //if (p_mesh == null)
        //    return;

        //p_mesh.vertices = p_vertices;
        //p_mesh.normals = p_normals;
        //p_mesh.uv = p_uv;

        //p_mesh.RecalculateBounds();

        //if(puissance2Resolution < 6)
        //{
        //    p_meshFilter.sharedMesh = p_mesh;
        //}


        //p_meshCollider.sharedMesh = null;
        //p_meshCollider.sharedMesh = p_mesh;

        //// Propagation des modification aux sous maillages LOD1 et LOD2
        //propagerLOD();
    }

    // --------------------------------------------------------------------
    // DEFORMATION PAR FONCTION
    // --------------------------------------------------------------------

    private void appliquerDeformation_Fonction()
    {
        if (p_vertices == null)
            return;

        switch (typeFonction)
        {
            case TypeFonction.Sinusoide:

                appliquerSinusoide();

                break;


            case TypeFonction.Collines:

                // Si aucun picking n'a ete effectue,
                // on place une colline au centre.
                Vector3 centre;

                if (p_pointPickingDisponible)
                {
                    centre = p_dernierPointPicking;
                }
                else
                {
                    centre = Vector3.zero;
                }

                appliquerColline(
                    centre.x,
                    centre.z
                );

                break;


            case TypeFonction.Perlin:

                p_seedPerlin =
                    Random.Range(0f, 10000f);

                appliquerPerlin();

                break;
        }

        recalculerToutesLesNormales();
        appliquerMesh();
    }

    // --------------------------------------------------------------------
    // SINUSOIDE
    // --------------------------------------------------------------------

    private void appliquerSinusoide()
    {
        if (p_vertices == null || p_vertices.Length == 0)
            return;

        NativeArray<Vector3> verticesNative = new NativeArray<Vector3>(p_vertices, Allocator.TempJob);

        SinusoideTerrainJob job = new SinusoideTerrainJob
        {
            vertices = verticesNative,
            frequence = 2f * Mathf.PI / dimension,
            hauteur = hauteurSinusoide
        };

        JobHandle handle = job.Schedule(verticesNative.Length, 64);
        handle.Complete();

        verticesNative.CopyTo(p_vertices);
        verticesNative.Dispose();
    }

    // --------------------------------------------------------------------
    // COLLINE GAUSSIENNE
    // --------------------------------------------------------------------

    private void appliquerColline(
        float centreX,
        float centreZ)
    {
        if (p_vertices == null || p_vertices.Length == 0)
            return;

        float sigma = Mathf.Max(0.01f, largeurColline);

        NativeArray<Vector3> verticesNative = new NativeArray<Vector3>(p_vertices, Allocator.TempJob);

        CollineTerrainJob job = new CollineTerrainJob
        {
            vertices = verticesNative,
            centreX = centreX,
            centreZ = centreZ,
            sigma = sigma,
            hauteur = hauteurColline
        };

        JobHandle handle = job.Schedule(verticesNative.Length, 64);
        handle.Complete();

        verticesNative.CopyTo(p_vertices);
        verticesNative.Dispose();
    }

    // --------------------------------------------------------------------
    // PERLIN NOISE
    // --------------------------------------------------------------------
    private void appliquerPerlin()
    {
        if (p_vertices == null || p_vertices.Length == 0)
            return;

        NativeArray<Vector3> verticesNative = new NativeArray<Vector3>(p_vertices, Allocator.TempJob);

        if (perlinBurst)
        {
            PerlinTerrainBurstJob jobBurst = new PerlinTerrainBurstJob
            {
                vertices = verticesNative,
                echelle = echellePerlin,
                seed = p_seedPerlin,
                hauteur = hauteurPerlin
            };

            jobBurst.Schedule(verticesNative.Length, 64).Complete();
        }
        else
        {
            PerlinTerrainJob job = new PerlinTerrainJob
            {
                vertices = verticesNative,
                echelle = echellePerlin,
                seed = p_seedPerlin,
                hauteur = hauteurPerlin
            };

            JobHandle handle = job.Schedule(verticesNative.Length, 64);
            handle.Complete();
        }

        verticesNative.CopyTo(p_vertices);
        verticesNative.Dispose();
    }

    // --------------------------------------------------------------------
    // DEFORMATION PAR HEIGHTMAP
    // --------------------------------------------------------------------
    private void appliquerDeformation_Texture()
    {
        if (p_vertices == null)
            return;

        if (textures == null || textures.Count == 0)
        {
            Debug.LogWarning("Aucune HeightMap n'est renseignee.");
            return;
        }

        numTexture = Mathf.Clamp(numTexture, 0, textures.Count - 1);
        Texture2D texture = textures[numTexture];

        if (texture == null)
        {
            Debug.LogWarning("La HeightMap selectionnee est vide.");
            return;
        }

        Color[] pixels;
        try
        {
            pixels = texture.GetPixels();
        }
        catch
        {
            Debug.LogError("Impossible de lire la HeightMap. Active Read/Write dans les proprietes de la texture.");
            return;
        }

        NativeArray<Color> pixelsNative = new NativeArray<Color>(pixels, Allocator.TempJob);
        NativeArray<Vector3> verticesNative = new NativeArray<Vector3>(p_vertices, Allocator.TempJob);

        TextureTerrainJob job = new TextureTerrainJob
        {
            pixels = pixelsNative,
            vertices = verticesNative,
            largeurImage = texture.width,
            hauteurImage = texture.height,
            resolution = resolution,
            hauteurMax = hauteurTexture
        };

        JobHandle handle = job.Schedule(verticesNative.Length, 16);
        handle.Complete();

        verticesNative.CopyTo(p_vertices);

        verticesNative.Dispose();
        pixelsNative.Dispose();

        recalculerToutesLesNormales();
        appliquerMesh();
    }

    // --------------------------------------------------------------------
    // TP5 : DEFORMATION PAR HEIGHTMAP EN ASYNCHRONE (F4)
    // --------------------------------------------------------------------

    private void lancerDeformationTextureAsync()
    {
        if (p_asyncEnCours || p_vertices == null || !p_voisinsNativesPrets)
            return;

        if (textures == null || textures.Count == 0)
        {
            Debug.LogWarning("Aucune HeightMap n'est renseignee.");
            return;
        }

        numTexture = Mathf.Clamp(numTexture, 0, textures.Count - 1);
        Texture2D texture = textures[numTexture];

        if (texture == null)
            return;

        System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();

        Color[] pixels;
        try
        {
            pixels = texture.GetPixels();
        }
        catch
        {
            Debug.LogError("Impossible de lire la HeightMap. Active Read/Write.");
            return;
        }

        p_asyncPixels = new NativeArray<Color>(pixels, Allocator.Persistent);
        p_asyncVertices = new NativeArray<Vector3>(p_vertices, Allocator.Persistent);
        p_asyncNormales = new NativeArray<Vector3>(p_vertices.Length, Allocator.Persistent);

        TextureTerrainJob jobTexture = new TextureTerrainJob
        {
            pixels = p_asyncPixels,
            vertices = p_asyncVertices,
            largeurImage = texture.width,
            hauteurImage = texture.height,
            resolution = resolution,
            hauteurMax = hauteurTexture
        };

        JobHandle h1 = jobTexture.Schedule(p_asyncVertices.Length, 64);

        NormalesVertexJob jobNormales = new NormalesVertexJob
        {
            vertices = p_asyncVertices,
            triangles = p_trianglesNative,
            debut = p_debutVoisinsNative,
            liste = p_listeVoisinsNative,
            mode = (int)modeNormale,
            normales = p_asyncNormales
        };

        // dependance : les normales attendent la fin du job texture
        p_handleAsync = jobNormales.Schedule(p_asyncVertices.Length, 64, h1);
        JobHandle.ScheduleBatchedJobs();

        p_asyncDebut = Time.realtimeSinceStartup;
        p_asyncEnCours = true;

        chrono.Stop();
        Debug.Log("Async lance : main thread bloque " +
                  chrono.Elapsed.TotalMilliseconds.ToString("F2") + " ms");
    }
    private void gererJobAsync()
    {
        if (!p_asyncEnCours || !p_handleAsync.IsCompleted)
            return;

        System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();

        p_handleAsync.Complete();                 // instantane : le job est deja fini
        p_asyncVertices.CopyTo(p_vertices);
        p_asyncNormales.CopyTo(p_normals);
        libererAsync();
        p_asyncEnCours = false;

        appliquerMesh();                          // upload mesh + collider (main thread)

        chrono.Stop();
        Debug.Log("Async termine : " +
                  ((Time.realtimeSinceStartup - p_asyncDebut) * 1000f).ToString("F0") +
                  " ms de bout en bout (sur plusieurs frames), " +
                  chrono.Elapsed.TotalMilliseconds.ToString("F2") +
                  " ms bloquants pour la recuperation");
    }
    private void libererAsync()
    {
        if (p_asyncVertices.IsCreated) p_asyncVertices.Dispose();
        if (p_asyncNormales.IsCreated) p_asyncNormales.Dispose();
        if (p_asyncPixels.IsCreated) p_asyncPixels.Dispose();
    }

    // --------------------------------------------------------------------
    // NORMALE D'UN TRIANGLE
    // --------------------------------------------------------------------
    private Vector3 calculerNormaleTriangle(
        Chunk chunk,
        int triangleIndex)
    {
        int i0 =
            chunk.triangles[triangleIndex];

        int i1 =
            chunk.triangles[triangleIndex + 1];

        int i2 =
            chunk.triangles[triangleIndex + 2];

        Vector3 v0 = chunk.vertices[i0];
        Vector3 v1 = chunk.vertices[i1];
        Vector3 v2 = chunk.vertices[i2];

        Vector3 V01 =
            v1 - v0;

        Vector3 V02 =
            v2 - v0;

        Vector3 normale =
            Vector3.Cross(V01, V02);


        if (normale.sqrMagnitude < 0.000001f)
            return Vector3.up;


        return normale.normalized;
    }

    // --------------------------------------------------------------------
    // SURFACE DU TRIANGLE
    // --------------------------------------------------------------------
    private float calculerSurfaceTriangle(
        Chunk chunk,
        int triangleIndex)
    {
        int i0 =
            chunk.triangles[triangleIndex];

        int i1 =
            chunk.triangles[triangleIndex + 1];

        int i2 =
            chunk.triangles[triangleIndex + 2];

        Vector3 v0 = chunk.vertices[i0];
        Vector3 v1 = chunk.vertices[i1];
        Vector3 v2 = chunk.vertices[i2];

        Vector3 a = v1 - v0;
        Vector3 b = v2 - v0;

        return Vector3.Cross(a, b).magnitude * 0.5f;
    }

    // --------------------------------------------------------------------
    // ANGLE DU TRIANGLE AU NIVEAU DU VERTEX
    // --------------------------------------------------------------------
    private float calculerAngleAuVertex(
        Chunk chunk,
        int triangleIndex,
        int vertex)
    {
        int i0 =
            chunk.triangles[triangleIndex];

        int i1 =
            chunk.triangles[triangleIndex + 1];

        int i2 =
            chunk.triangles[triangleIndex + 2];

        int autre1;
        int autre2;

        if (vertex == i0)
        {
            autre1 = i1;
            autre2 = i2;
        }
        else if (vertex == i1)
        {
            autre1 = i0;
            autre2 = i2;
        }
        else
        {
            autre1 = i0;
            autre2 = i1;
        }

        Vector3 a =
            chunk.vertices[autre1]
            -
            chunk.vertices[vertex];

        Vector3 b =
            chunk.vertices[autre2]
            -
            chunk.vertices[vertex];

        if (a.sqrMagnitude < 0.000001f ||
            b.sqrMagnitude < 0.000001f)
        {
            return 0f;
        }

        return Vector3.Angle(a, b);
    }
    // --------------------------------------------------------------------
    // CALCUL DE LA NORMALE D'UN VERTEX (version C# sequentielle d'origine)
    // --------------------------------------------------------------------

    //private void calculerNormaleVertex(
    //    uint num_Vertex)
    //{
    //    int vertex =
    //        (int)num_Vertex;


    //    if (vertex < 0 ||
    //        vertex >= p_vertices.Length)
    //        return;


    //    List<int> triangles =
    //        p_trianglesParVertex[vertex];


    //    if (triangles == null ||
    //        triangles.Count == 0)
    //    {
    //        p_normals[vertex] =
    //            Vector3.up;

    //        return;
    //    }


    //    Vector3 normaleFinale =
    //        Vector3.zero;


    //    // ------------------------------------------------------------
    //    // A - Moyenne simple
    //    // ------------------------------------------------------------

    //    if (modeNormale == ModeNormale.Basique)
    //    {
    //        foreach (int triangleIndex in triangles)
    //        {
    //            normaleFinale +=
    //                calculerNormaleTriangle(
    //                    triangleIndex
    //                );
    //        }
    //    }


    //    // ------------------------------------------------------------
    //    // B - Moyenne pondérée par la surface
    //    // ------------------------------------------------------------

    //    else if (modeNormale == ModeNormale.Surface)
    //    {
    //        foreach (int triangleIndex in triangles)
    //        {
    //            Vector3 normale =
    //                calculerNormaleTriangle(
    //                    triangleIndex
    //                );

    //            float surface =
    //                calculerSurfaceTriangle(
    //                    triangleIndex
    //                );

    //            normaleFinale +=
    //                normale * surface;
    //        }
    //    }


    //    // ------------------------------------------------------------
    //    // C - Moyenne pondérée par l'angle
    //    // ------------------------------------------------------------

    //    else if (modeNormale == ModeNormale.Angle)
    //    {
    //        foreach (int triangleIndex in triangles)
    //        {
    //            Vector3 normale =
    //                calculerNormaleTriangle(
    //                    triangleIndex
    //                );

    //            float angle =
    //                calculerAngleAuVertex(
    //                    triangleIndex,
    //                    vertex
    //                );

    //            normaleFinale +=
    //                normale * angle;
    //        }
    //    }


    //    if (normaleFinale.sqrMagnitude <
    //        0.000001f)
    //    {
    //        p_normals[vertex] =
    //            Vector3.up;
    //    }
    //    else
    //    {
    //        p_normals[vertex] =
    //            normaleFinale.normalized;
    //    }
    //}

    // --------------------------------------------------------------------
    // RECALCUL DE TOUTES LES NORMALES (job + Burst, repli sequentiel)
    // --------------------------------------------------------------------

    private void recalculerToutesLesNormales()
    {
        if (p_chunks.Count == 0 || p_vertices == null)
            return;

        if (p_chunks.Count > 1)
        {
            foreach (Chunk chunk in p_chunks.Values)
                recalculerNormalesChunk(chunk);
            return;
        }

        if (!utiliserJobNormales || !p_voisinsNativesPrets)
        {
            recalculerNormalesChunk(chunkBase);
            return;
        }

        NativeArray<Vector3> verticesNative =
            new NativeArray<Vector3>(p_vertices, Allocator.TempJob);

        NativeArray<Vector3> normalesNative =
            new NativeArray<Vector3>(
                p_vertices.Length,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);

        NormalesVertexJob job = new NormalesVertexJob
        {
            vertices = verticesNative,
            triangles = p_trianglesNative,
            debut = p_debutVoisinsNative,
            liste = p_listeVoisinsNative,
            mode = (int)modeNormale,
            normales = normalesNative
        };

        job.Schedule(p_vertices.Length, 64).Complete();

        normalesNative.CopyTo(p_normals);

        verticesNative.Dispose();
        normalesNative.Dispose();
    }

    private bool effectuerPicking(
        out RaycastHit hit)
    {
        hit = new RaycastHit();

        if (p_cam == null)
            return false;

        if (Mouse.current == null)
            return false;

        Vector2 positionSouris =
            Mouse.current.position.ReadValue();

        Ray rayon =
            p_cam.ScreenPointToRay(
                positionSouris
            );

        return Physics.Raycast(
            rayon,
            out hit,
            Mathf.Infinity,
            maskPickingTerrain
        );
    }

    // --------------------------------------------------------------------
    // MESURE DES PERFORMANCES DES 4 JOBS + NORMALES
    // --------------------------------------------------------------------

    private const int repetitionsMesure = 100;
    private const int lotMesure = 64;

    private double mesurerSinusoideSequentiel()
    {
        if (p_vertices == null || p_vertices.Length == 0)
            return -1.0;

        System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();
        float frequence = 2f * Mathf.PI / dimension;

        for (int rep = 0; rep < repetitionsMesure; rep++)
        {
            Vector3[] verticesTest = (Vector3[])p_vertices.Clone();

            for (int index = 0; index < verticesTest.Length; index++)
            {
                Vector3 v = verticesTest[index];
                v.y = Mathf.Sin(v.x * frequence * 2f)
                    * Mathf.Sin(v.z * frequence * 2f)
                    * hauteurSinusoide;
                verticesTest[index] = v;
            }
        }
        chrono.Stop();
        return chrono.Elapsed.TotalMilliseconds / repetitionsMesure;
    }

    private double mesurerSinusoideJob()
    {
        if (p_vertices == null || p_vertices.Length == 0)
            return -1.0;

        System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();
        float frequence = 2f * Mathf.PI / dimension;

        for (int rep = 0; rep < repetitionsMesure; rep++)
        {
            NativeArray<Vector3> verticesNative =
                new NativeArray<Vector3>(p_vertices, Allocator.TempJob);

            SinusoideTerrainJob job = new SinusoideTerrainJob
            {
                vertices = verticesNative,
                frequence = frequence,
                hauteur = hauteurSinusoide
            };

            JobHandle handle = job.Schedule(verticesNative.Length, lotMesure);
            handle.Complete();

            verticesNative.CopyTo(p_vertices);   // recopie retour mesuree aussi
            verticesNative.Dispose();
        }

        chrono.Stop();
        return chrono.Elapsed.TotalMilliseconds / repetitionsMesure;
    }
    private double mesurerCollineSequentiel()
    {
        if (p_vertices == null || p_vertices.Length == 0)
            return -1.0;

        float centreX = 0f;
        float centreZ = 0f;
        float sigma = Mathf.Max(0.01f, largeurColline);

        System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();

        for (int rep = 0; rep < repetitionsMesure; rep++)
        {
            Vector3[] verticesTest = (Vector3[])p_vertices.Clone();

            for (int index = 0; index < verticesTest.Length; index++)
            {
                Vector3 v = verticesTest[index];
                float dx = v.x - centreX;
                float dz = v.z - centreZ;
                float distanceCarree = dx * dx + dz * dz;
                float facteur = Mathf.Exp(-distanceCarree / (2f * sigma * sigma));
                v.y = hauteurColline * facteur;
                verticesTest[index] = v;
            }
        }

        chrono.Stop();
        return chrono.Elapsed.TotalMilliseconds / repetitionsMesure;
    }

    private double mesurerCollineJob()
    {
        if (p_vertices == null || p_vertices.Length == 0)
            return -1.0;

        float centreX = 0f;
        float centreZ = 0f;
        float sigma = Mathf.Max(0.01f, largeurColline);

        System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();

        for (int rep = 0; rep < repetitionsMesure; rep++)
        {
            NativeArray<Vector3> verticesNative =
                new NativeArray<Vector3>(p_vertices, Allocator.TempJob);

            CollineTerrainJob job = new CollineTerrainJob
            {
                vertices = verticesNative,
                centreX = centreX,
                centreZ = centreZ,
                sigma = sigma,
                hauteur = hauteurColline
            };

            JobHandle handle = job.Schedule(verticesNative.Length, lotMesure);
            handle.Complete();

            verticesNative.CopyTo(p_vertices);
            verticesNative.Dispose();
        }
        chrono.Stop();
        return chrono.Elapsed.TotalMilliseconds / repetitionsMesure;
    }
    private double mesurerPerlinSequentiel()
    {
        if (p_vertices == null || p_vertices.Length == 0)
            return -1.0;

        float seed = p_seedPerlin;

        System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();

        for (int rep = 0; rep < repetitionsMesure; rep++)
        {
            Vector3[] verticesTest = (Vector3[])p_vertices.Clone();

            for (int index = 0; index < verticesTest.Length; index++)
            {
                Vector3 v = verticesTest[index];
                float x = v.x * echellePerlin + seed;
                float z = v.z * echellePerlin + seed;
                float bruit = Mathf.PerlinNoise(x, z);
                v.y = (bruit - 0.5f) * 2f * hauteurPerlin;
                verticesTest[index] = v;
            }
        }

        chrono.Stop();
        return chrono.Elapsed.TotalMilliseconds / repetitionsMesure;
    }
    private double mesurerPerlinJob()
    {
        if (p_vertices == null || p_vertices.Length == 0)
            return -1.0;

        float seed = p_seedPerlin;

        System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();

        for (int rep = 0; rep < repetitionsMesure; rep++)
        {
            NativeArray<Vector3> verticesNative =
                new NativeArray<Vector3>(p_vertices, Allocator.TempJob);

            PerlinTerrainJob job = new PerlinTerrainJob
            {
                vertices = verticesNative,
                echelle = echellePerlin,
                seed = seed,
                hauteur = hauteurPerlin
            };

            JobHandle handle = job.Schedule(verticesNative.Length, lotMesure);
            handle.Complete();

            verticesNative.CopyTo(p_vertices);
            verticesNative.Dispose();
        }

        chrono.Stop();
        return chrono.Elapsed.TotalMilliseconds / repetitionsMesure;
    }
    private double mesurerPerlinJobBurst()
    {
        if (p_vertices == null || p_vertices.Length == 0)
            return -1.0;

        float seed = p_seedPerlin;

        System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();

        for (int rep = 0; rep < repetitionsMesure; rep++)
        {
            NativeArray<Vector3> verticesNative =
                new NativeArray<Vector3>(p_vertices, Allocator.TempJob);

            PerlinTerrainBurstJob job = new PerlinTerrainBurstJob
            {
                vertices = verticesNative,
                echelle = echellePerlin,
                seed = seed,
                hauteur = hauteurPerlin
            };

            JobHandle handle = job.Schedule(verticesNative.Length, lotMesure);
            handle.Complete();

            verticesNative.CopyTo(p_vertices);
            verticesNative.Dispose();
        }

        chrono.Stop();
        return chrono.Elapsed.TotalMilliseconds / repetitionsMesure;
    }
    private double mesurerTextureSequentiel()
    {
        if (p_vertices == null || p_vertices.Length == 0)
            return -1.0;

        if (textures == null || textures.Count == 0 || textures[numTexture] == null)
            return -1.0;

        Texture2D texture = textures[numTexture];
        Color[] pixels;

        try
        {
            pixels = texture.GetPixels();
        }
        catch
        {
            UnityEngine.Debug.LogError(
                "Impossible de mesurer la HeightMap : active Read/Write dans les proprietes de la texture.");
            return -1.0;
        }

        System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();

        for (int rep = 0; rep < repetitionsMesure; rep++)
        {
            Vector3[] verticesTest = (Vector3[])p_vertices.Clone();

            for (int index = 0; index < verticesTest.Length; index++)
            {
                int i = index % resolution;
                int j = index / resolution;

                float u = (float)i / (resolution - 1);
                float v = (float)j / (resolution - 1);

                float px = u * (texture.width - 1);
                float py = v * (texture.height - 1);

                int x0 = (int)px;
                int y0 = (int)py;
                int x1 = Mathf.Min(x0 + 1, texture.width - 1);
                int y1 = Mathf.Min(y0 + 1, texture.height - 1);

                float tx = px - x0;
                float ty = py - y0;

                Color c00 = pixels[y0 * texture.width + x0];
                Color c10 = pixels[y0 * texture.width + x1];
                Color c01 = pixels[y1 * texture.width + x0];
                Color c11 = pixels[y1 * texture.width + x1];

                Color cx0 = Color.Lerp(c00, c10, tx);
                Color cx1 = Color.Lerp(c01, c11, tx);
                Color couleur = Color.Lerp(cx0, cx1, ty);

                Vector3 vertex = verticesTest[index];
                vertex.y = couleur.grayscale * hauteurTexture;
                verticesTest[index] = vertex;
            }
        }

        chrono.Stop();
        return chrono.Elapsed.TotalMilliseconds / repetitionsMesure;
    }
    private double mesurerTextureJob()
    {
        if (p_vertices == null || p_vertices.Length == 0)
            return -1.0;

        if (textures == null || textures.Count == 0 || textures[numTexture] == null)
            return -1.0;

        Texture2D texture = textures[numTexture];
        Color[] pixels;

        try
        {
            pixels = texture.GetPixels();
        }
        catch
        {
            UnityEngine.Debug.LogError(
                "Impossible de mesurer la HeightMap : active Read/Write dans les proprietes de la texture.");
            return -1.0;
        }

        System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();

        for (int rep = 0; rep < repetitionsMesure; rep++)
        {
            NativeArray<Color> pixelsNative =
                new NativeArray<Color>(pixels, Allocator.TempJob);

            NativeArray<Vector3> verticesNative =
                new NativeArray<Vector3>(p_vertices, Allocator.TempJob);

            TextureTerrainJob job = new TextureTerrainJob
            {
                pixels = pixelsNative,
                vertices = verticesNative,
                largeurImage = texture.width,
                hauteurImage = texture.height,
                resolution = resolution,
                hauteurMax = hauteurTexture
            };

            JobHandle handle = job.Schedule(verticesNative.Length, lotMesure);
            handle.Complete();

            verticesNative.CopyTo(p_vertices);
            verticesNative.Dispose();
            pixelsNative.Dispose();
        }

        chrono.Stop();
        return chrono.Elapsed.TotalMilliseconds / repetitionsMesure;
    }
    // Normales : parallele = Schedule (multithread), sinon Run (main thread).
    // Avec Burst actif, Run() isole l'effet "Burst seul".
    private double mesurerNormalesJob(int repetitions, bool parallele)
    {
        System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();

        for (int r = 0; r < repetitions; r++)
        {
            NativeArray<Vector3> vN = new NativeArray<Vector3>(p_vertices, Allocator.TempJob);
            NativeArray<Vector3> nN = new NativeArray<Vector3>(
                p_vertices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            NormalesVertexJob job = new NormalesVertexJob
            {
                vertices = vN,
                triangles = p_trianglesNative,
                debut = p_debutVoisinsNative,
                liste = p_listeVoisinsNative,
                mode = (int)modeNormale,
                normales = nN
            };

            if (parallele)
                job.Schedule(p_vertices.Length, lotMesure).Complete();
            else
                job.Run(p_vertices.Length);

            nN.CopyTo(p_normals);   // recopie retour mesuree aussi
            vN.Dispose();
            nN.Dispose();
        }

        chrono.Stop();
        return chrono.Elapsed.TotalMilliseconds / repetitions;
    }
    private void mesurerNormales()
    {
        if (p_vertices == null || !p_voisinsNativesPrets || chunkBase == null)
        {
            Debug.LogWarning("Voisins natifs non prets.");
            return;
        }

        const int rep = 10;

        System.Diagnostics.Stopwatch chrono =
            System.Diagnostics.Stopwatch.StartNew();

        for (int r = 0; r < rep; r++)
            recalculerNormalesChunk(chunkBase);

        chrono.Stop();

        double sequentiel =
            chrono.Elapsed.TotalMilliseconds / rep;

        double burstSeul =
            mesurerNormalesJob(rep, false);

        double jobs =
            mesurerNormalesJob(rep, true);

        Debug.Log(
            "NORMALES (" + modeNormale + ", " + p_vertices.Length + " vertices)\n" +
            "  C# sequentiel : " + sequentiel.ToString("F2") + " ms\n" +
            "  Job via Run() : " + burstSeul.ToString("F2") + " ms\n" +
            "  Job via Schedule() : " + jobs.ToString("F2") + " ms"
        );
    }

    private void afficherResultatMesure(string nom, double tempsSequentiel, double tempsParallele)
    {
        if (tempsSequentiel < 0.0 || tempsParallele < 0.0)
        {
            UnityEngine.Debug.Log(nom + " : mesure impossible.");
            return;
        }

        double gain = tempsParallele > 0.0
            ? tempsSequentiel / tempsParallele
            : 0.0;

        UnityEngine.Debug.Log(
            nom + "\n" +
            "  Sequentiel : " + tempsSequentiel.ToString("F3") + " ms / repetition\n" +
            "  Parallele  : " + tempsParallele.ToString("F3") + " ms / repetition\n" +
            "  Gain       : " + gain.ToString("F2") + " x");
    }

    // --------------------------------------------------------------------
    // TP5/TP6 : MESURES COMPLEMENTAIRES
    // --------------------------------------------------------------------

    private bool burstEstActif()
    {
        NativeArray<int> r = new NativeArray<int>(1, Allocator.TempJob);
        SondeBurstJob sonde = new SondeBurstJob { resultat = r };
        sonde.Run();
        bool actif = r[0] == 1;
        r.Dispose();
        return actif;
    }

    private string libelleModeBurst()
    {
        return burstEstActif() ? "Jobs + Burst (Burst ACTIF)" : "Jobs seul (Burst INACTIF)";
    }

    // Section 5 du TP : "Hello World" (y = constante). Gain attendu : aucun.
    private void mesurerReinitialisation()
    {
        if (p_vertices == null || p_vertices.Length == 0)
            return;

        int n = p_vertices.Length;
        Vector3[] cible = new Vector3[n];

        System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();
        for (int rep = 0; rep < repetitionsMesure; rep++)
        {
            Vector3[] test = (Vector3[])p_vertices.Clone();
            for (int i = 0; i < test.Length; i++)
                test[i].y = 0f;
        }
        double sequentiel = chrono.Elapsed.TotalMilliseconds / repetitionsMesure;

        chrono.Restart();
        for (int rep = 0; rep < repetitionsMesure; rep++)
        {
            NativeArray<Vector3> verticesNative = new NativeArray<Vector3>(p_vertices, Allocator.TempJob);

            ReinitialiserTerrainJob job = new ReinitialiserTerrainJob
            {
                vertices_job = verticesNative,
                hauteurCst_job = 0f
            };
            job.Schedule(verticesNative.Length, lotMesure).Complete();

            verticesNative.CopyTo(cible);     // on ne modifie pas le terrain
            verticesNative.Dispose();
        }
        double parallele = chrono.Elapsed.TotalMilliseconds / repetitionsMesure;

        afficherResultatMesure("HELLO WORLD : remise a plat y=0 (traitement trop leger)", sequentiel, parallele);
    }
    private void mesurerInfluenceLot()
    {
        if (p_vertices == null || p_vertices.Length == 0)
            return;

        const int repetitionsLot = 50;
        int[] lots = { 8, 16, 32, 64, 128, 256, 1024, 4096 };
        float frequence = 2f * Mathf.PI / dimension;

        string rapport = "INFLUENCE DU LOT - SINUSOIDE (" + p_vertices.Length + " vertices, " +
                         libelleModeBurst() + ", " + repetitionsLot + " repetitions)\n";

        for (int l = 0; l < lots.Length; l++)
        {
            System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();

            for (int rep = 0; rep < repetitionsLot; rep++)
            {
                NativeArray<Vector3> verticesNative = new NativeArray<Vector3>(p_vertices, Allocator.TempJob);

                SinusoideTerrainJob job = new SinusoideTerrainJob
                {
                    vertices = verticesNative,
                    frequence = frequence,
                    hauteur = hauteurSinusoide
                };
                job.Schedule(verticesNative.Length, lots[l]).Complete();

                verticesNative.Dispose();
            }

            rapport += "  lot = " + lots[l].ToString().PadLeft(5) + " : " +
                       (chrono.Elapsed.TotalMilliseconds / repetitionsLot).ToString("F3") + " ms\n";
        }

        Debug.Log(rapport);
    }
    private void mesurerBakerVoisins()
    {
        if (p_triangles == null || p_vertices == null)
            return;

        const int rep = 5;
        int nbV = p_vertices.Length;

        System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();
        for (int r = 0; r < rep; r++)
        {
            int[] d;
            int[] l;
            construireVoisinsCSharp(nbV, out d, out l);
        }
        double csharp = chrono.Elapsed.TotalMilliseconds / rep;

        chrono.Restart();
        for (int r = 0; r < rep; r++)
        {
            NativeArray<int> tri = new NativeArray<int>(p_triangles, Allocator.TempJob);
            NativeArray<int> debut = new NativeArray<int>(nbV + 1, Allocator.TempJob);
            NativeArray<int> liste = new NativeArray<int>(p_triangles.Length, Allocator.TempJob);

            VoisinsCsrJob job = new VoisinsCsrJob
            {
                triangles = tri,
                nbVertices = nbV,
                debut = debut,
                liste = liste
            };
            job.Run();

            tri.Dispose();
            debut.Dispose();
            liste.Dispose();
        }
        double burst = chrono.Elapsed.TotalMilliseconds / rep;

        Debug.Log(
            "VOISINAGE CSR (code non parallelise, " + nbV + " vertices)\n" +
            "  C# manage          : " + csharp.ToString("F2") + " ms\n" +
            "  IJob.Run() (" + (burstEstActif() ? "Burst" : "C# via job, Burst inactif") + ") : " +
            burst.ToString("F2") + " ms\n" +
            "  Gain               : " + (burst > 0.0 ? (csharp / burst).ToString("F2") : "?") + " x");
    }

    // --------------------------------------------------------------------
    // F6 : LIMITES MEMOIRE / TEST DE CHARGE
    // --------------------------------------------------------------------
    private void mesurerLimitesMemoire()
    {
        const float MO = 1024f * 1024f;

        long managee = System.GC.GetTotalMemory(false);
        long alloueUnity = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        long reserveUnity = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();

        int nbV = p_vertices != null ? p_vertices.Length : 0;
        int nbIdx = p_triangles != null ? p_triangles.Length : 0;

        // estimation des tableaux du terrain : vertices+normales (12 o), uv (8 o), indices (4 o)
        // + 3 tableaux natifs du voisinage (4 o par entier)
        long tableauxManages = (long)nbV * (12 + 12 + 8) + (long)nbIdx * 4;
        long tableauxNatifs = ((long)nbIdx + (long)nbIdx + (long)nbV + 1) * 4;

        Debug.Log(
            "========== ETAT MEMOIRE DU TERRAIN ==========\n" +
            "Mode : " + libelleModeBurst() + "\n" +
            "Resolution : " + resolution + " x " + resolution + "  (" + nbV + " vertices, " +
            (nbIdx / 3) + " triangles)\n" +
            "Tableaux C# du terrain (estim.)  : " + (tableauxManages / MO).ToString("F0") + " Mo\n" +
            "Tableaux natifs voisinage (estim): " + (tableauxNatifs / MO).ToString("F0") + " Mo\n" +
            "Memoire C# manage (GC)           : " + (managee / MO).ToString("F0") + " Mo\n" +
            "Memoire Unity allouee / reservee : " + (alloueUnity / MO).ToString("F0") + " / " +
            (reserveUnity / MO).ToString("F0") + " Mo\n" +
            "FPS (lisses) : " + p_fps.ToString("F0"));

        int[] resolutions = { 1024, 2048, 4096 };
        string rapport = "========== TEST DE CHARGE SINUSOIDE (grilles synthetiques, calcul seul) ==========\n" +
                         "Mode : " + libelleModeBurst() + "\n";

        for (int r = 0; r < resolutions.Length; r++)
        {
            int res = resolutions[r];
            int n = res * res;
            float pas = dimension / (res - 1);
            float frequence = 2f * Mathf.PI / dimension;

            rapport += res + " x " + res + " = " + (n / 1000000f).ToString("F1") + " M vertices (" +
                       (n * 12L / MO).ToString("F0") + " Mo par tableau) : ";

            Vector3[] seq = null;
            NativeArray<Vector3> natif = default(NativeArray<Vector3>);

            try
            {
                seq = new Vector3[n];
                System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();
                for (int index = 0; index < n; index++)
                {
                    float x = (index / res) * pas;
                    float z = (index % res) * pas;
                    float y = Mathf.Sin(x * frequence * 2f)
                            * Mathf.Sin(z * frequence * 2f)
                            * hauteurSinusoide;
                    seq[index] = new Vector3(x, y, z);
                }
                double tSeq = chrono.Elapsed.TotalMilliseconds;
                seq = null;
                System.GC.Collect();

                // --- Jobs (+ Burst si actif) : 1 passe d'echauffement puis 3 mesures ---
                natif = new NativeArray<Vector3>(n, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                double tJob = 0.0;
                for (int passe = 0; passe < 4; passe++)
                {
                    chrono.Restart();

                    GrilleSyntheseJob grille = new GrilleSyntheseJob
                    {
                        vertices = natif,
                        resolution = res,
                        pas = pas
                    };
                    JobHandle h1 = grille.Schedule(n, 64);

                    SinusoideTerrainJob sinus = new SinusoideTerrainJob
                    {
                        vertices = natif,
                        frequence = frequence,
                        hauteur = hauteurSinusoide
                    };
                    sinus.Schedule(n, 64, h1).Complete();

                    if (passe > 0)
                        tJob += chrono.Elapsed.TotalMilliseconds / 3.0;
                }

                rapport += "sequentiel " + tSeq.ToString("F0") + " ms | jobs " + tJob.ToString("F1") +
                           " ms | gain x" + (tJob > 0.0 ? (tSeq / tJob).ToString("F1") : "?") + "\n";
            }
            catch (System.Exception e)
            {
                rapport += "ECHEC (memoire ?) : " + e.GetType().Name + "\n";
            }
            finally
            {
                if (natif.IsCreated)
                    natif.Dispose();
                seq = null;
                System.GC.Collect();
            }
        }

        Debug.Log(rapport + "========================================================================");
    }
    private void mesurerPerformances4Jobs()
    {
        if (p_vertices == null || p_vertices.Length == 0)
        {
            UnityEngine.Debug.LogWarning(
                "Impossible de mesurer : le terrain n'est pas initialise.");
            return;
        }

        UnityEngine.Debug.Log(
            "========== COMPARAISON SEQUENTIEL / PARALLELE ==========\n" +
            "Vertices : " + p_vertices.Length + "\n" +
            "Repetitions : " + repetitionsMesure + "\n" +
            "Lot Jobs : " + lotMesure + "\n" +
            "Mode : " + libelleModeBurst() + "\n" +
            "(Jobs seul <-> Jobs+Burst : menu Jobs > Burst > Enable Compilation)");

        double sinusoideSeq = mesurerSinusoideSequentiel();
        double sinusoideJob = mesurerSinusoideJob();

        double collineSeq = mesurerCollineSequentiel();
        double collineJob = mesurerCollineJob();

        double perlinSeq = mesurerPerlinSequentiel();
        double perlinJob = mesurerPerlinJob();
        double perlinJobBurst = mesurerPerlinJobBurst();

        double textureSeq = mesurerTextureSequentiel();
        double textureJob = mesurerTextureJob();

        afficherResultatMesure("SINUSOIDE", sinusoideSeq, sinusoideJob);
        afficherResultatMesure("COLLINE", collineSeq, collineJob);
        afficherResultatMesure("PERLIN (Mathf.PerlinNoise, Jobs seul)", perlinSeq, perlinJob);
        afficherResultatMesure("PERLIN (cnoise, Jobs + Burst)", perlinSeq, perlinJobBurst);
        afficherResultatMesure("TEXTURE", textureSeq, textureJob);
        mesurerNormales();
        mesurerReinitialisation();
        mesurerBakerVoisins();
        mesurerInfluenceLot();

        UnityEngine.Debug.Log("========================================================");
    }

    // --------------------------------------------------------------------
    // UPDATE
    // --------------------------------------------------------------------

    void Update()
    {
        gererJobAsync();
        p_fps = Mathf.Lerp(p_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.05f);
        gererClavier();
        gererCamera();
        //gererPicking();
        gererSculptureTerrain();
        gererAffichageNormales();
        gererDoubleF11();
    }

    // --------------------------------------------------------------------
    // CLAVIER
    // --------------------------------------------------------------------

    private void gererClavier()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.f1Key.wasPressedThisFrame)
            p_afficherAide = !p_afficherAide;

        // F2 : changement de fonction
        if (Keyboard.current.f2Key.wasPressedThisFrame && !p_asyncEnCours)
        {
            if (p_chunks.Count > 1)
                Debug.LogWarning("Les fonctions sont reservees au terrain non etendu.");
            else
            {
                choixModeDeformation = ChoixModeDeformation.Fonction;
                typeFonction = (TypeFonction)(((int)typeFonction + 1) % 3);
                appliquerDeformation_Fonction();
            }
        }

        // F3 : HeightMap classique
        if (Keyboard.current.f3Key.wasPressedThisFrame && !p_asyncEnCours)
        {
            if (p_chunks.Count > 1)
                Debug.LogWarning("Les HeightMaps sont reservees au terrain non etendu.");
            else
            {
                choixModeDeformation = ChoixModeDeformation.Texture;

                if (textures != null && textures.Count > 0)
                    numTexture = (numTexture + 1) % textures.Count;

                appliquerDeformation_Texture();
            }
        }

        // F4 : HeightMap asynchrone
        if (Keyboard.current.f4Key.wasPressedThisFrame && !p_asyncEnCours)
        {
            if (p_chunks.Count > 1)
                Debug.LogWarning("Le traitement asynchrone est reserve au terrain non etendu.");
            else
            {
                choixModeDeformation = ChoixModeDeformation.Texture;

                if (textures != null && textures.Count > 0)
                    numTexture = (numTexture + 1) % textures.Count;

                lancerDeformationTextureAsync();
            }
        }

        // F5 : mesures des 4 traitements
        if (Keyboard.current.f5Key.wasPressedThisFrame && !p_asyncEnCours)
            mesurerPerformances4Jobs();

        // F6 : memoire + test de charge
        if (Keyboard.current.f6Key.wasPressedThisFrame && !p_asyncEnCours)
            mesurerLimitesMemoire();

        // F10 : affichage des normales
        if (Keyboard.current.f10Key.wasPressedThisFrame)
        {
            p_modeAffichageNormales++;

            if (p_modeAffichageNormales > 3)
                p_modeAffichageNormales = 0;

            p_tempsAffichageNormales = DUREE_AFFICHAGE_NORMALES;
        }

        // F12 : methode de calcul des normales
        if (Keyboard.current.f12Key.wasPressedThisFrame && !p_asyncEnCours)
        {
            modeNormale = (ModeNormale)(((int)modeNormale + 1) % 3);

            recalculerToutesLesNormales();
            appliquerMesh();

            Debug.Log("Mode normale : " + modeNormale);
        }

        // Z : changer de metrique de distance
        if (Keyboard.current.zKey.wasPressedThisFrame)
        {
            distanceUtilisee = (TypeDistance)(((int)distanceUtilisee + 1) % 4);
            Debug.Log("Distance utilisee : " + distanceUtilisee);
        }

        // Fleches : extension du terrain
        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
            etendreTerrain(Vector2Int.up);

        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
            etendreTerrain(Vector2Int.down);

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
            etendreTerrain(Vector2Int.left);

        if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
            etendreTerrain(Vector2Int.right);
    }

    private void gererDoubleF11()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.f11Key.wasPressedThisFrame && !p_asyncEnCours)
        {
            float maintenant =
                Time.time;

            if (maintenant -
                p_dernierAppuiF11
                <= DELAI_DOUBLE_F11)
            {
                remettreTerrainPlat();

                p_dernierAppuiF11 = -10f;
            }
            else
            {
                p_dernierAppuiF11 =
                    maintenant;
            }
        }
    }

    // --------------------------------------------------------------------
    // PICKING
    // --------------------------------------------------------------------

    private void gererPicking()
    {
        if (Mouse.current == null)
            return;


        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;


        if (!effectuerPicking(out RaycastHit hit))
            return;


        p_dernierPointPicking =
            hit.point;

        p_pointPickingDisponible = true;

        // Une colline est ajoutee
        // uniquement lorsque le mode fonction
        // est sur Collines.
        if (choixModeDeformation ==
            ChoixModeDeformation.Fonction &&
            typeFonction ==
            TypeFonction.Collines)
        {
            appliquerColline(
                hit.point.x,
                hit.point.z
            );

            recalculerToutesLesNormales();
            appliquerMesh();
        }
    }

    // --------------------------------------------------------------------
    // CAMERA
    // --------------------------------------------------------------------
    private void gererCamera()
    {
        if (p_cam == null)
            return;

        if (Keyboard.current == null)
            return;

        float delta = vitesseCamera * Time.deltaTime;
        Vector3 direction = Vector3.zero;

        // --- DEPLACEMENT ZQSD PHYSIQUE (Touches QWERTY WASD) ---
        if (Keyboard.current.wKey.isPressed) // AZERTY Z (Avancer)
            direction += p_cam.transform.forward;

        if (Keyboard.current.sKey.isPressed) // AZERTY S (Reculer)
            direction -= p_cam.transform.forward;

        if (Keyboard.current.dKey.isPressed) // AZERTY D (Droite)
            direction += p_cam.transform.right;

        if (Keyboard.current.aKey.isPressed) // AZERTY Q (Gauche)
            direction -= p_cam.transform.right;

        if (Keyboard.current.eKey.isPressed) // AZERTY E (Monter)
            direction += Vector3.up;

        if (Keyboard.current.qKey.isPressed) // AZERTY A (Descendre)
            direction -= Vector3.up;

        if (direction.sqrMagnitude > 0.001f)
        {
            p_cam.transform.position += direction.normalized * delta;
        }

        // --- ROTATION OKLM PHYSIQUE (Touches QWERTY O, K, L, ;) ---
        float rotation = vitesseRotationCamera * Time.deltaTime;

        if (Keyboard.current.kKey.isPressed) // AZERTY K (Gauche)
        {
            p_cam.transform.Rotate(Vector3.up, -rotation, Space.World);
        }

        if (Keyboard.current.semicolonKey.isPressed) // AZERTY M (Droite, touche ";" en QWERTY)
        {
            p_cam.transform.Rotate(Vector3.up, rotation, Space.World);
        }

        if (Keyboard.current.oKey.isPressed) // AZERTY O (Haut)
        {
            p_cam.transform.Rotate(Vector3.right, -rotation, Space.Self);
        }

        if (Keyboard.current.lKey.isPressed) // AZERTY L (Bas)
        {
            p_cam.transform.Rotate(Vector3.right, rotation, Space.Self);
        }

        // R tourne le terrain. (Le R est a la meme place en AZERTY et QWERTY)
        if (Keyboard.current.rKey.isPressed)
        {
            transform.Rotate(
                Vector3.up,
                vitesseRotationTerrain * Time.deltaTime,
                Space.World
            );
        }
    }

    // --------------------------------------------------------------------
    // AFFICHAGE DES NORMALES
    // --------------------------------------------------------------------

    private void gererAffichageNormales()
    {
        if (p_tempsAffichageNormales <= 0f)
            return;

        p_tempsAffichageNormales -=
            Time.deltaTime;

        if (p_vertices == null)
            return;

        // Pour eviter de dessiner plusieurs milliers
        // de lignes a chaque frame.
        int pas =
            Mathf.Max(
                1,
                p_vertices.Length / 1000
            );

        for (int i = 0;
             i < p_vertices.Length;
             i += pas)
        {
            Vector3 origine =
                transform.TransformPoint(
                    p_vertices[i]
                );


            Vector3 direction =
                Vector3.zero;

            // --------------------------------------------------------
            // Mode 0 : normales vertices
            // --------------------------------------------------------

            if (p_modeAffichageNormales == 0)
            {
                direction =
                    transform.TransformDirection(
                        p_normals[i]
                    );
            }

            // --------------------------------------------------------
            // Mode 1 : normale d'eclairage
            // moyenne des normales des 3 vertices
            // --------------------------------------------------------

            else if (p_modeAffichageNormales == 1)
            {
                direction =
                    calculerNormaleEclairage(i);
            }

            // --------------------------------------------------------
            // Mode 2 : normale d'orientation
            // --------------------------------------------------------

            else if (p_modeAffichageNormales == 2)
            {
                direction =
                    calculerNormaleOrientation(i);
            }

            // --------------------------------------------------------
            // Mode 3 :
            // orientation + eclairage
            // --------------------------------------------------------

            else
            {
                Vector3 normaleEclairage =
                    calculerNormaleEclairage(i);

                Vector3 normaleOrientation =
                    calculerNormaleOrientation(i);

                Debug.DrawLine(
                    origine,
                    origine +
                    normaleEclairage * 2f,
                    Color.green
                );

                Debug.DrawLine(
                    origine,
                    origine +
                    normaleOrientation * 2f,
                    Color.red
                );

                continue;
            }
            Debug.DrawLine(
                origine,
                origine +
                direction * 2f,
                Color.yellow
            );
        }
    }
    // --------------------------------------------------------------------
    // NORMALE D'ECLAIRAGE
    // --------------------------------------------------------------------
    private Vector3 calculerNormaleEclairage(
        int vertex)
    {
        List<int> triangles =
            p_trianglesParVertex[vertex];

        Vector3 resultat =
            Vector3.zero;

        int nombre = 0;

        foreach (int triangleIndex in triangles)
        {
            int a =
                p_triangles[triangleIndex];

            int b =
                p_triangles[triangleIndex + 1];

            int c =
                p_triangles[triangleIndex + 2];

            Vector3 normaleA =
                p_normals[a];

            Vector3 normaleB =
                p_normals[b];

            Vector3 normaleC =
                p_normals[c];


            resultat +=
                normaleA +
                normaleB +
                normaleC;
            nombre += 3;
        }
        if (nombre == 0)
            return Vector3.up;

        return transform.TransformDirection(
            (resultat / nombre).normalized
        );
    }


    private Vector3 calculerNormaleTriangle(int triangleIndex) => calculerNormaleTriangle(chunkBase, triangleIndex);
    // --------------------------------------------------------------------
    // NORMALE D'ORIENTATION
    // --------------------------------------------------------------------
    private Vector3 calculerNormaleOrientation(
        int vertex)
    {
        List<int> triangles =
            p_trianglesParVertex[vertex];


        if (triangles == null ||
            triangles.Count == 0)
            return transform.up;


        int triangleIndex =
            triangles[0];

        
        Vector3 normale =
            calculerNormaleTriangle(
                triangleIndex
            );


        return transform.TransformDirection(
            normale
        );
    }
    // --------------------------------------------------------------------
    // GUI
    // --------------------------------------------------------------------
    private void OnGUI()
    {
        // FPS toujours visibles (TP5)
        GUI.Label(
            new Rect(10, 5, 200, 20),
            "FPS : " + p_fps.ToString("F0")
        );

        if (!p_afficherAide)
            return;

        GUI.Box(
            new Rect(
                20,
                30,
                460,
                1100
            ),
            ""
        );

        GUILayout.BeginArea(
            new Rect(
                40,
                45,
                420,
                1090
            )
        );

        GUILayout.Label("INFORMATIONS DU MAILLAGE");

        if (p_vertices != null)
        {
            GUILayout.Label($@"Vertices : {p_vertices.Length}   |   Triangles : {p_triangles.Length / 3}");
            GUILayout.Label($"Resolution : {resolution} x {resolution}   |   Memoire approximative : {calculerMemoireMesh()} Ko");
        }
        GUILayout.Label("PARAMETRES");
        GUILayout.Label($"Mode : {choixModeDeformation}   |   Fonction : {typeFonction}");
        GUILayout.Label($"Normales : {modeNormale}   |   Distance : {distanceUtilisee}");

        GUILayout.Space(10);
        GUILayout.Label("INTERACTIONS");
        GUILayout.Label("F1 : Aide / informations");
        GUILayout.Label("F2 : Fonction suivante   |   F3 / F4 : HeightMap suivante (F4 async)");
        GUILayout.Label("F5 : Mesurer les Jobs / Burst / normales");
        GUILayout.Label("F6 : Memoire + test de charge (grilles 1024 a 4096)");
        GUILayout.Label("F10 : Afficher les normales   |   F11 x2 : Revenir au terrain plat");
        GUILayout.Label("F12 : Changer le calcul des normales");
        GUILayout.Label("Z : Changer de distance (Exercice 2)");
        GUILayout.Label("ZQSD : Deplacer la camera   |   E / A : Monter / Descendre");
        GUILayout.Label("O/K/L/M : Tourner la camera   |   R : Faire tourner le terrain");

        GUILayout.Space(10);
        GUILayout.Label("SCULPTURE INTERACTIVE (EXERCICE 1)");
        GUILayout.Label($"Pattern Actif : {p_indexPatternCourant}   |   Intensite Max : {intensiteMaxDeformation}   |   Rayon : {rayonDeformation}");
        GUILayout.Label("Clic Gauche / Droit : Sculpter (Elevation / Depression)");
        GUILayout.Label("Molette + SHIFT / CTRL / ALT : Varier intensite / rayon / pattern");

        GUILayout.Space(10);
        GUILayout.Label("EXERCICE 2 - DISTANCES");
        GUILayout.Label("W : Euclidienne / Euclidienne carree / Manhattan / Chebyshev");
        GUILayout.Label($"Espace : Visualiser le voisinage   |   Voisins : {p_nombreVoisins}");

        GUILayout.Space(10);
        GUILayout.Label("PARAMETRES LOD :");

        // Affichage si le LOD est actif ou pas
        LODGroup lodGroup = GetComponent<LODGroup>();
        if (lodGroup != null)
        {
            LOD[] lods = lodGroup.GetLODs();
            string status = "LOD Inactif / Hors champs";

            for (int i = 0; i < lods.Length; i++)
            {
                if (lods[i].renderers.Length > 0 && lods[i].renderers[0] != null)
                {
                    status = $"LOD Actif : LOD_{i}";
                    break;
                }
            }
            GUILayout.Space(10);
            GUILayout.Label(status);
        }

        // Afficher le nombre de chunks total et dimention du terrain
        long totVertices = 0;
        long totTriangles = 0;
        long totMemory = 0;

        foreach (Chunk chunk in p_chunks.Values)
        {
            totVertices += chunk.vertices.Length;
            totTriangles += chunk.triangles.Length / 3;
            totMemory += (chunk.vertices.Length * 12L + chunk.normals.Length * 12L + chunk.uv.Length * 8L + chunk.triangles.Length * 4L) / 1024L;
        }
        int width = gridMax.x - gridMin.x + 1;
        int height = gridMax.y - gridMin.y + 1;
        
        GUILayout.Space(10);
        GUILayout.Label($"Chunks : {p_chunks.Count}, Total Memory : {totMemory} Ko, Total Vertices : {totVertices}, Total Triangles : {totTriangles} (Dimensions : {width} x {height})");

        GUILayout.EndArea();
    }

    // --------------------------------------------------------------------
    // MEMOIRE
    // --------------------------------------------------------------------

    private long calculerMemoireMesh()
    {
        if (p_vertices == null)
            return 0;

        long memoire =
            p_vertices.Length *
            3 *
            sizeof(float);

        memoire +=
            p_normals.Length *
            3 *
            sizeof(float);

        memoire +=
            p_uv.Length *
            2 *
            sizeof(float);

        memoire +=
            p_triangles.Length *
            sizeof(int);

        return memoire / 1024;
    }

    // --------------------------------------------------------------------
    // SCULPTURE DU TERRAIN
    // --------------------------------------------------------------------

    private void gererSculptureTerrain()
    {
        if (Mouse.current == null)
            return;

        if (Keyboard.current == null)
            return;

        // TP5 : pas de sculpture pendant un job asynchrone
        // (p_vertices sera ecrase par le resultat du job).
        if (p_asyncEnCours)
            return;

        // ============================================================
        // MOLETTE
        // ============================================================

        float scrollY =
            Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scrollY) > 0.01f)
        {
            Debug.Log("MOLETTE = " + scrollY);

            // --------------------------------------------------------
            // SHIFT + MOLETTE = INTENSITE
            // --------------------------------------------------------

            if (Keyboard.current.shiftKey.isPressed)
            {
                float variation =
                    Mathf.Sign(scrollY);


                intensiteMaxDeformation +=
                    variation;


                intensiteMaxDeformation =
                    Mathf.Max(
                        1f,
                        intensiteMaxDeformation
                    );


                Debug.Log(
                    "SHIFT + MOLETTE -> Intensite = "
                    + intensiteMaxDeformation
                );
            }

            // --------------------------------------------------------
            // CTRL + MOLETTE = RAYON
            // --------------------------------------------------------

            else if (Keyboard.current.ctrlKey.isPressed)
            {
                float variation =
                    Mathf.Sign(scrollY);


                rayonDeformation +=
                    variation;


                rayonDeformation =
                    Mathf.Max(
                        1f,
                        rayonDeformation
                    );


                Debug.Log(
                    "CTRL + MOLETTE -> Rayon = "
                    + rayonDeformation
                );
            }


            // --------------------------------------------------------
            // ALT + MOLETTE = PATTERN
            // --------------------------------------------------------

            else if (Keyboard.current.altKey.isPressed)
            {
                if (patternsDeformation != null &&
                    patternsDeformation.Length > 0)
                {
                    if (scrollY > 0)
                    {
                        p_indexPatternCourant++;

                        if (p_indexPatternCourant >=
                            patternsDeformation.Length)
                        {
                            p_indexPatternCourant = 0;
                        }
                    }
                    else
                    {
                        p_indexPatternCourant--;

                        if (p_indexPatternCourant < 0)
                        {
                            p_indexPatternCourant =
                                patternsDeformation.Length - 1;
                        }
                    }


                    Debug.Log(
                        "ALT + MOLETTE -> Pattern = "
                        + p_indexPatternCourant
                    );
                }
            }
        }

        // ============================================================
        // VERIFICATION DU PATTERN
        // ============================================================

        if (patternsDeformation == null ||
            patternsDeformation.Length == 0)
        {
            return;
        }

        if (p_indexPatternCourant >=
            patternsDeformation.Length)
        {
            p_indexPatternCourant = 0;
        }


        if (p_indexPatternCourant < 0)
        {
            p_indexPatternCourant =
                patternsDeformation.Length - 1;
        }

        // ============================================================
        // CLIC / ESPACE
        // ============================================================

        bool clicGauche =
            Mouse.current.leftButton.isPressed;

        bool clicDroit =
            Mouse.current.rightButton.isPressed;

        bool espace =
            Keyboard.current.spaceKey.isPressed;


        if (clicGauche ||
            clicDroit ||
            espace)
        {
            if (effectuerPicking(out RaycastHit hit))
            {
                if (espace)
                {
                    previsualiserDeformation(
                        hit.point
                    );
                }
                else
                {
                    appliquerPatternDeformation(
                        hit.point,
                        clicGauche
                    );
                }
            }
        }
    }
    // --------------------------------------------------------------------
    // EXERCICE 2 : CALCUL DES DISTANCES
    // --------------------------------------------------------------------
    private float calculerDistanceNormalisee(Vector3 vertex, Vector3 centre, out bool dansRayon)
    {
        float dx = vertex.x - centre.x;
        float dz = vertex.z - centre.z;
        float rayon = Mathf.Max(0.01f, rayonDeformation);

        switch (distanceUtilisee)
        {
            case TypeDistance.Euclidienne:
                {
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    dansRayon = d < rayon;
                    return d / rayon;
                }
            case TypeDistance.EuclidienneCarree:
                {
                    float dc = dx * dx + dz * dz;        // pas de racine
                    float rayonCarre = rayon * rayon;
                    dansRayon = dc < rayonCarre;
                    return dc / rayonCarre;
                }
            case TypeDistance.Manhattan:
                {
                    float d = Mathf.Abs(dx) + Mathf.Abs(dz);
                    dansRayon = d < rayon;
                    return d / rayon;
                }
            default: // Chebyshev
                {
                    float d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz));
                    dansRayon = d < rayon;
                    return d / rayon;
                }
        }
    }
    // --------------------------------------------------------------------
    // PATTERN DE DEFORMATION
    // --------------------------------------------------------------------
    private void appliquerPatternDeformation(Vector3 pointMonde, bool elevation)
    {
        if (p_vertices == null)
            return;

        if (patternsDeformation == null || patternsDeformation.Length == 0)
            return;

        AnimationCurve courbe = patternsDeformation[p_indexPatternCourant];

        if (courbe == null)
        {
            Debug.LogWarning("Le pattern " + p_indexPatternCourant + " est vide.");
            return;
        }

        // Point de collision en espace local
        float direction = elevation ? 1f : -1f;
        float forceMax = intensiteMaxDeformation * Time.deltaTime * 5f;

        List<Chunk> touches = chunkTouches(pointMonde);
        
        foreach (Chunk chunk in touches)
        {
            Vector3 centreLocal = chunk.go.transform.InverseTransformPoint(pointMonde);
            for (int i = 0; i < chunk.vertices.Length; i++)
            {
                float distanceNormalisee = calculerDistanceNormalisee(chunk.vertices[i], centreLocal, out bool dansRayon);
                if (!dansRayon)
                    continue;
                chunk.vertices[i].y += direction * courbe.Evaluate(distanceNormalisee) * forceMax;

            }
        }

        synchroniserJumeaux(touches);
        foreach (Chunk chunk in touches)
        {
            recalculerNormalesChunk(chunk);
        }

        foreach (Chunk chunk in touches)
        {
            appliquerMeshChunk(chunk);
        }
    }


    private void synchroniserJumeaux (List<Chunk> liste)
    {
        foreach (Chunk chunk in liste)
        {
            int last = chunk.vertices.Length;
            for (int k = 0; k < chunk.res; k++)
            {
                synchroniserVertex(chunk, k, 0);
                synchroniserVertex(chunk, k, last);
                synchroniserVertex(chunk, 0, k);
                synchroniserVertex(chunk, last, k);
            }
        }
    }

    private void synchroniserVertex(Chunk chunk, int index_x, int index_z)
    {
        trouverJumeaux(chunk, index_x, index_z);
        if(p_membresTmp.Count == 0)
            return;
        
        Chunk reference = chunk;
        int iRef = index_z * chunk.res + index_x;
        for (int k = 0; k < p_membresTmp.Count; k++)
        {
            Chunk jumeau = p_membresTmp[k];
            if (jumeau.coord.y < reference.coord.y || (jumeau.coord.y == reference.coord.y && jumeau.coord.x < reference.coord.x))
            {
                reference = jumeau;
                iRef = p_indicesTmp[k];
            }
            
        }

        float yRef = reference.vertices[iRef].y;
        chunk.vertices[index_z * chunk.res + index_x].y = yRef;
        for (int k = 0; k < p_membresTmp.Count; k++)
        {
            Chunk jumeau = p_membresTmp[k];
            int iJum = p_indicesTmp[k];
            jumeau.vertices[iJum].y = yRef;
        }
    }

    // --------------------------------------------------------------------
    // PREVISUALISATION DE LA DEFORMATION
    // --------------------------------------------------------------------
    private void previsualiserDeformation(Vector3 pointMonde)
    {
        if (p_vertices == null)
            return;

        Vector3 centreLocal =
            transform.InverseTransformPoint(pointMonde);
        p_nombreVoisins = 0;


        for (int i = 0; i < p_vertices.Length; i++)
        {
            bool dansRayon;

            calculerDistanceNormalisee(
                p_vertices[i],
                centreLocal,
                out dansRayon
            );

            if (dansRayon)
            {
                Vector3 positionMondeVertex =
                    transform.TransformPoint(p_vertices[i]);

                Debug.DrawLine(
                    positionMondeVertex,
                    positionMondeVertex + Vector3.up * 1f,
                    Color.magenta
                );

                p_nombreVoisins++;
            }
        }
    }
}
