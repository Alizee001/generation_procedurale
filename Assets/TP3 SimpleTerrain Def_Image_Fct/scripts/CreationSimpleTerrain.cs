using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[BurstCompile(CompileSynchronously = true)]
public struct SinusoideTerrainBurstJob : IJobParallelFor
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

// Version Jobs sans Burst : sert de comparaison TP5.
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
public struct CollineTerrainBurstJob : IJobParallelFor
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

// Version Jobs sans Burst : sert de comparaison TP5.
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

// Même calcul Perlin pour comparer correctement C# / Jobs / Jobs+Burst.
public struct PerlinTerrainJob : IJobParallelFor
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

// Version Jobs sans Burst : même interpolation bilinéaire que la version Burst.
public struct TextureTerrainJob : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<Color> pixels;

    public NativeArray<Vector3> vertices;
    public int largeurImage;
    public int hauteurImage;
    public int resolution;
    public float hauteurMax;
    public float uDebut, vDebut, uEchelle, vEchelle;

    public void Execute(int index)
    {
        int i = index % resolution;
        int j = index / resolution;

        float u = uDebut + ((float)i / (resolution - 1)) * uEchelle;
        float v = vDebut + ((float)j / (resolution - 1)) * vEchelle;

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
public struct TextureTerrainBurstJob : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<Color> pixels;

    public NativeArray<Vector3> vertices;
    public int largeurImage;
    public int hauteurImage;
    public int resolution;
    public float hauteurMax;
    public float uDebut, vDebut, uEchelle, vEchelle;

    public void Execute(int index)
    {
        int i = index % resolution;
        int j = index / resolution;

        float u = uDebut + ((float)i / (resolution - 1)) * uEchelle;
        float v = vDebut + ((float)j / (resolution - 1)) * vEchelle;

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

        // Interpolation manuelle pour rester simple et Burst-compatible.
        float r0 = c00.r + (c10.r - c00.r) * tx;
        float g0 = c00.g + (c10.g - c00.g) * tx;
        float b0 = c00.b + (c10.b - c00.b) * tx;

        float r1 = c01.r + (c11.r - c01.r) * tx;
        float g1 = c01.g + (c11.g - c01.g) * tx;
        float b1 = c01.b + (c11.b - c01.b) * tx;

        float r = r0 + (r1 - r0) * ty;
        float g = g0 + (g1 - g0) * ty;
        float b = b0 + (b1 - b0) * ty;

        float gris = r * 0.299f + g * 0.587f + b * 0.114f;

        Vector3 vertex = vertices[index];
        vertex.y = gris * hauteurMax;
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

    // EXERCICE 2 : METRIQUES DE DISTANCE
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

    public enum ModeExecution
    {
        Sequentiel,
        Jobs,
        JobsBurst
    }

    [Header("TP5 - Jobs / Burst")]
    public ModeExecution modeExecution = ModeExecution.JobsBurst;

    [Tooltip("innerloopBatchCount des jobs")]
    public int lotJobs = 64;

    [Tooltip("Nombre d'appels pour la moyenne des temps")]
    public int repetitionsMesure = 100;

    [Tooltip("Normales calculees par job (sinon ancienne boucle C#)")]
    public bool utiliserJobNormales = true;

    private Mesh meshLOD0, meshLOD1, meshLOD2;

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

    [Header("Sculpture Interactive (Exercice 1)")]
    [Tooltip("Ajoutez des courbes allant de X=0 a X=1, et Y=0 a Y=1")]
    public AnimationCurve[] patternsDeformation;
    public float rayonDeformation = 15f;
    public float intensiteMaxDeformation = 10f;
    private int p_indexPatternCourant = 0;

    // Donnees internes
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
    private float p_cameraPitch;
    [SerializeField] private float sensibiliteSourisCamera = 0.15f;

    private LayerMask maskPickingTerrain;

    private float p_dimInterVertices;

    // Liste des triangles attachés à chaque vertex.
    //private List<int>[] p_trianglesParVertex;

    // Voisinage aplati en memoire native (format CSR) pour le job des normales.
    private NativeArray<int> p_trianglesNative;
    private NativeArray<int> p_debutVoisinsNative;
    private NativeArray<int> p_listeVoisinsNative;
    private bool p_voisinsNativesPrets = false;

    // Traitement asynchrone (F4)
    private bool p_asyncEnCours = false;
    private JobHandle p_handleAsync;
    private class AsyncChunk
    {
        public Chunk chunk;
        public NativeArray<Vector3> vertices;
        public NativeArray<Vector3> normales;
    }
    private readonly List<AsyncChunk> p_asyncChunks = new List<AsyncChunk>();

    private NativeArray<Color> p_asyncPixels;
    private float p_asyncDebut;

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

    // UI (Canvas construit par script)
    private GameObject p_panneauAide;
    private Text p_texteFps, p_texteGauche, p_texteDroite;
    private float p_prochaineMajUI;
    private readonly StringBuilder p_sb = new StringBuilder(2048);
    private readonly List<string> p_derniersResultats = new List<string>();
    private bool p_burstActif;

    // Pour le Perlin.
    private float p_seedPerlin;

    // Rotation terrain.
    private bool p_rotationTerrain = false;

    // EXERCICE 2 : affichage des distances
    private bool p_afficherDistances = false;
    private Vector3 p_centreDistanceMonde;
    private float p_distanceMaxAffichee = 0f;
    private int p_nombreVoisins = 0;

    [Header("Exercice 3 - LOD")]

    [Tooltip("Seuils LOD0 -> LOD1 -> LOD2 -> culling")]
    public float[] seuilsLOD =
{
    0.5f,
    0.15f,
    0.02f
};

    [Tooltip("Importance minimale pour mettre à jour un LOD")]
    public float seuilImportanceLOD = 0.5f;

    [Tooltip("Marge d'anticipation des mises à jour LOD")]
    public float margeAnticipationLOD = 1.3f;

    [Tooltip("Délai entre deux mises à jour LOD")]
    public float delaiMajLODSec = 0.25f;

    [Tooltip("Délai entre deux mises à jour du collider")]
    public float delaiMajColliderSec = 0.1f;

    // TP5 : traitement asynchrone (F4)
    private NativeArray<Vector3> p_asyncVertices;
    private NativeArray<Vector3> p_asyncNormales;

    // TP5 : FPS lisses
    private float p_fps;

    private class ZoneModifiee
    {
        public int minX, maxX, minZ, maxZ;
        public float importance;

        public bool EstVide => maxX < minX;

        public ZoneModifiee()
        {
            Vider();
        }

        public void Vider()
        {
            minX = int.MaxValue;
            minZ = int.MaxValue;
            maxX = -1;
            maxZ = -1;
            importance = 0f;
        }

        public void Etendre(
            int x0,
            int x1,
            int z0,
            int z1,
            float apport)
        {
            minX = Mathf.Min(minX, x0);
            maxX = Mathf.Max(maxX, x1);
            minZ = Mathf.Min(minZ, z0);
            maxZ = Mathf.Max(maxZ, z1);
            importance += apport;
        }

        public void Tout(int res)
        {
            minX = 0;
            minZ = 0;
            maxX = res - 1;
            maxZ = res - 1;
            importance = float.MaxValue;
        }
    }

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

        public LODGroup lodGroup;

        public ZoneModifiee zone1 = new ZoneModifiee();
        public ZoneModifiee zone2 = new ZoneModifiee();

        public bool colliderPerime;
        public float dernierMajLOD;
        public float dernierMajCollider;
    }

    Dictionary<Vector2Int, Chunk> p_chunks = new Dictionary<Vector2Int, Chunk>();
    Vector2Int gridMin = Vector2Int.zero;
    Vector2Int gridMax = Vector2Int.zero;
    private Material p_materialBase;
    private readonly List<Chunk> p_membresTmp = new List<Chunk>();
    private readonly List<int> p_indicesTmp = new List<int>();

    private Chunk chunkBase => p_chunks.TryGetValue(Vector2Int.zero, out Chunk c) ? c : null;
    private Vector3 decalageChunk(Chunk c) => new Vector3(c.coord.x * dimension, 0f, c.coord.y * dimension);
    private Vector3[] p_vertices => chunkBase?.vertices;
    private Vector3[] p_normals => chunkBase?.normals;
    private Vector2[] p_uv => chunkBase?.uv;
    private int[] p_triangles => chunkBase?.triangles;
    private List<int>[] p_trianglesParVertex => chunkBase?.trianglesParVertex;


    private bool p_surbrillance = false;

    // RESET

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

    // AWAKE
    void Awake()
    {
        p_cam = Camera.main;

        if (p_cam == null)
            p_cam = FindFirstObjectByType<Camera>();

        if (p_cam != null)
            p_cameraPitch = p_cam.transform.localEulerAngles.x;

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
    // START

    void Start()
    {
        creerChunk(Vector2Int.zero);

        resolution = (ushort)(1 << puissance2Resolution);
        creerUI();
        p_burstActif = burstEstActif();

        //if (puissance2Resolution >= 6)
        //{
        //    initialiserLODGroup();
        //}
        //else
        //{
        //    creerLeMeshTerrain();
        //}

    }
    // LIBERATION DE LA MEMOIRE NATIVE

    void OnDestroy()
    {
        p_handleAsync.Complete();
        libererAsync();
        libererVoisinsNatifs();
    }

    // INITIALISATION DU GROUPE LOD
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

   // CREATION DES ENFANTS DES LOD
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

    private List<Chunk> chunkTouches(Vector3 pointMonde)
    {
        List<Chunk> chunks = new List<Chunk>();
        float origine = CentrerPivot ? dimension * 0.5f : 0f;
        foreach (Chunk chunk in p_chunks.Values)
        {
            float marg = rayonDeformation + 2f * dimension / (chunk.res - 1);
            Vector3 localPoint = chunk.go.transform.InverseTransformPoint(pointMonde);
            if (localPoint.x >= -origine - marg && localPoint.x <= dimension - origine + marg &&
                localPoint.z >= -origine - marg && localPoint.z <= dimension - origine + marg)
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

        // On enregistre d’abord le chunk dans le dictionnaire.
        // Cela permet à chunkBase / p_vertices de pointer correctement
        // vers le chunk (0,0) lorsque l’on construit les buffers natifs.
        p_chunks[coord] = chunk;

        bakerVoisins(chunk);

        // Les buffers natifs utilisés par les jobs TP5 correspondent
        // au chunk de référence (0,0), pas à chaque chunk du terrain.
        if (coord == Vector2Int.zero)
            bakerVoisinsNatif();

        // Les vertices frontières reprennent position ET normale de leurs jumeaux existants
        int last = chunk.res - 1;
        for (int i = 0; i < chunk.res; i++)
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
                p_indicesTmp.Add(voisinz * voisin.res + voisinx);
            }
        }
    }



    // CALCUL DU MAPPING
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


    // Recalcule seulement les normales des 4 bords d'un chunk (somme aussi les triangles des jumeaux).
    // Utilise apres un calcul par job, qui ne voit que les triangles d'un seul chunk.
    private void recalculerNormalesBords(Chunk chunk)
    {
        int last = chunk.res - 1;
        recalculerNormalesZone(chunk, 0, last, 0, 0);
        recalculerNormalesZone(chunk, 0, last, last, last);
        recalculerNormalesZone(chunk, 0, 0, 0, last);
        recalculerNormalesZone(chunk, last, last, 0, last);
    }

    // Normales d'un chunk par NormalesVertexJob. Tous les chunks ont la meme resolution,
    // donc la meme topologie : le CSR natif du chunk (0,0) sert pour tous.
    private void normalesParJob(Chunk chunk)
    {
        int n = chunk.vertices.Length;
        NativeArray<Vector3> vN = new NativeArray<Vector3>(chunk.vertices, Allocator.TempJob);
        NativeArray<Vector3> nN = new NativeArray<Vector3>(n, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        new NormalesVertexJob
        {
            vertices = vN,
            triangles = p_trianglesNative,
            debut = p_debutVoisinsNative,
            liste = p_listeVoisinsNative,
            mode = (int)modeNormale,
            normales = nN
        }.Schedule(n, lotJobs).Complete();

        nN.CopyTo(chunk.normals);
        vN.Dispose();
        nN.Dispose();
    }

    // Touche C : un materiau different par chunk pendant 3 s
    private IEnumerator surligneChunks()
    {
        p_surbrillance = true;
        List<Material> temp = new List<Material>();
        int n = 0;

        foreach (Chunk chunk in p_chunks.Values)
        {
            Material mats = new Material(p_materialBase);
            mats.color = Color.HSVToRGB((n++ * 0.17f) % 1f, 0.7f, 1f);
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


    // BAKING DES VOISINS

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

        // Le voisinage natif du chunk de référence est construit dans creerChunk()
        // après l’enregistrement du chunk dans p_chunks.
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



    // LOD_0 = maillage maitre (deforme + collider). LOD_1 intermediaire, LOD_2 = 16x16.
    private void creerLODsChunk(Chunk chunk)
    {
        int resLOD1 = 1 << (4 + (puissance2Resolution - 4) / 2);
        int resLOD2 = 16;

        chunk.lodGroup = chunk.go.AddComponent<LODGroup>();
        Renderer r0 = creerEnfantsLOD(chunk.go.transform, "LOD_0", chunk.res, out chunk.mesh, out chunk.vertices, out chunk.normals, out chunk.uv, out chunk.triangles);
        Renderer r1 = creerEnfantsLOD(chunk.go.transform, "LOD_1", resLOD1, out chunk.mesh1, out chunk.vertices1, out chunk.normals1, out _, out _);
        Renderer r2 = creerEnfantsLOD(chunk.go.transform, "LOD_2", resLOD2, out chunk.mesh2, out chunk.vertices2, out chunk.normals2, out _, out _);

        chunk.lodGroup.SetLODs(new LOD[]
        {
            new LOD(seuilsLOD[0], new Renderer[] { r0 }),
            new LOD(seuilsLOD[1], new Renderer[] { r1 }),
            new LOD(seuilsLOD[2], new Renderer[] { r2 })
        });
        chunk.lodGroup.RecalculateBounds();

        // Correspondance sommet degrade -> sommet LOD0, calculee une seule fois (baking)
        calculerMapping(chunk.res, resLOD1, out chunk.map1);
        calculerMapping(chunk.res, resLOD2, out chunk.map2);
    }

    // Hauteur relative a l'ecran du chunk : meme grandeur que celle utilisee par le LODGroup.
    private float hauteurEcranRelative(Chunk chunk)
    {
        if (p_cam == null || chunk.lodGroup == null)
            return 1f;

        Vector3 centre = chunk.go.transform.TransformPoint(chunk.lodGroup.localReferencePoint);
        Vector3 echelle = chunk.go.transform.lossyScale;
        float taille = chunk.lodGroup.size * Mathf.Max(echelle.x, Mathf.Max(echelle.y, echelle.z));
        float distance = Mathf.Max(Vector3.Distance(p_cam.transform.position, centre), 0.01f);
        float hauteurVue = 2f * distance * Mathf.Tan(p_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return taille / hauteurVue;
    }

    // Niveau de LOD actif d'apres les seuils ; -1 = chunk non affiche (cull)
    private int niveauLODActif(Chunk chunk)
    {
        if (chunk.lodGroup == null)
            return 0;
        float h = hauteurEcranRelative(chunk);
        for (int i = 0; i < seuilsLOD.Length; i++)
            if (h >= seuilsLOD[i])
                return i;
        return -1;
    }

    private int resolutionLOD(Chunk chunk, int niveau)
    {
        if (niveau == 0) return chunk.res;
        if (niveau == 1) return chunk.mesh1 != null ? Mathf.RoundToInt(Mathf.Sqrt(chunk.vertices1.Length)) : chunk.res;
        return chunk.mesh2 != null ? Mathf.RoundToInt(Mathf.Sqrt(chunk.vertices2.Length)) : chunk.res;
    }

    // Recopie hauteurs + normales de LOD0 vers un LOD degrade, SEULEMENT dans la zone modifiee.
    private void propagerZone(Chunk chunk, Mesh mesh, Vector3[] v, Vector3[] n, int[] map, ZoneModifiee zone)
    {
        int resBasse = Mathf.RoundToInt(Mathf.Sqrt(v.Length));
        float k = (resBasse - 1) / (float)(chunk.res - 1);

        int x0 = Mathf.Max(0, Mathf.FloorToInt(zone.minX * k) - 1);
        int x1 = Mathf.Min(resBasse - 1, Mathf.CeilToInt(zone.maxX * k) + 1);
        int z0 = Mathf.Max(0, Mathf.FloorToInt(zone.minZ * k) - 1);
        int z1 = Mathf.Min(resBasse - 1, Mathf.CeilToInt(zone.maxZ * k) + 1);

        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                int i = z * resBasse + x;
                int source = map[i];
                v[i].y = chunk.vertices[source].y;
                n[i] = chunk.normals[source];
            }
        }

        mesh.vertices = v;
        mesh.normals = n;
        mesh.RecalculateBounds();
        zone.Vider();
        chunk.dernierMajLOD = Time.time;
    }

    // Mise a jour immediate et complete (F2, F3, F11, F12, async, creation de chunk)
    private void propagerLODComplet(Chunk chunk)
    {
        if (chunk.mesh1 == null)
            return;
        chunk.zone1.Tout(chunk.res);
        chunk.zone2.Tout(chunk.res);
        propagerZone(chunk, chunk.mesh1, chunk.vertices1, chunk.normals1, chunk.map1, chunk.zone1);
        propagerZone(chunk, chunk.mesh2, chunk.vertices2, chunk.normals2, chunk.map2, chunk.zone2);
    }

    // QUAND mettre a jour (appelee a chaque frame) :
    //  - la zone doit etre non vide et son importance cumulee >= seuilImportanceLOD
    //  - le LOD doit etre actif ou sur le point de l'etre (hauteur ecran vs seuils x marge)
    //  - on est hors geste (boutons relaches) ou le delai delaiMajLODSec est ecoule
    private void gererMisesAJourDifferees()
    {
        bool geste = Mouse.current != null &&
                     (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed);

        foreach (Chunk chunk in p_chunks.Values)
        {
            if (chunk.colliderPerime && (!geste || Time.time - chunk.dernierMajCollider >= delaiMajColliderSec))
                rafraichirCollider(chunk);

            if (chunk.mesh1 == null || (chunk.zone1.EstVide && chunk.zone2.EstVide))
                continue;
            if (geste && Time.time - chunk.dernierMajLOD < delaiMajLODSec)
                continue;

            float h = hauteurEcranRelative(chunk);

            // LOD1 visible pour h dans [seuil1, seuil0[ ; LOD2 pour h dans [seuil2, seuil1[
            bool besoinLOD1 = h < seuilsLOD[0] * margeAnticipationLOD && h >= seuilsLOD[1] / margeAnticipationLOD;
            bool besoinLOD2 = h < seuilsLOD[1] * margeAnticipationLOD && h >= seuilsLOD[2] / margeAnticipationLOD;

            if (besoinLOD1 && !chunk.zone1.EstVide && chunk.zone1.importance >= seuilImportanceLOD)
                propagerZone(chunk, chunk.mesh1, chunk.vertices1, chunk.normals1, chunk.map1, chunk.zone1);

            if (besoinLOD2 && !chunk.zone2.EstVide && chunk.zone2.importance >= seuilImportanceLOD)
                propagerZone(chunk, chunk.mesh2, chunk.vertices2, chunk.normals2, chunk.map2, chunk.zone2);
        }
    }

    private void rafraichirCollider(Chunk chunk)
    {
        if (chunk.collider != null)
        {
            chunk.collider.sharedMesh = null;
            chunk.collider.sharedMesh = chunk.mesh;
        }
        chunk.colliderPerime = false;
        chunk.dernierMajCollider = Time.time;
    }

    // Mise a jour complete d'un chunk (generation, remise a plat, normales...)
    private void appliquerMeshChunk(Chunk chunk)
    {
        chunk.mesh.vertices = chunk.vertices;
        chunk.mesh.normals = chunk.normals;
        chunk.mesh.RecalculateBounds();
        rafraichirCollider(chunk);
        propagerLODComplet(chunk);
    }

    private void appliquerMesh()
    {
        foreach (Chunk chunk in p_chunks.Values)
            appliquerMeshChunk(chunk);
    }

    // Mise a jour legere pendant la sculpture : LOD0 tout de suite, collider et LOD differes.
    private void appliquerMeshChunkSculpture(Chunk chunk, int x0, int x1, int z0, int z1, float importance)
    {
        chunk.mesh.vertices = chunk.vertices;
        chunk.mesh.normals = chunk.normals;
        chunk.mesh.RecalculateBounds();
        chunk.colliderPerime = true;
        chunk.zone1.Etendre(x0, x1, z0, z1, importance);
        chunk.zone2.Etendre(x0, x1, z0, z1, importance);
    }

    // ======================================================================
    //  NORMALES (TP3 : 3 modes, TP5 : job)
    // ======================================================================
    private Vector3 calculerNormaleTriangle(Chunk chunk, int triangleIndex)
    {
        Vector3 v0 = chunk.vertices[chunk.triangles[triangleIndex]];
        Vector3 v1 = chunk.vertices[chunk.triangles[triangleIndex + 1]];
        Vector3 v2 = chunk.vertices[chunk.triangles[triangleIndex + 2]];
        Vector3 n = Vector3.Cross(v1 - v0, v2 - v0);
        return n.sqrMagnitude < 0.000001f ? Vector3.up : n.normalized;
    }

    private float calculerSurfaceTriangle(Chunk chunk, int triangleIndex)
    {
        Vector3 v0 = chunk.vertices[chunk.triangles[triangleIndex]];
        Vector3 v1 = chunk.vertices[chunk.triangles[triangleIndex + 1]];
        Vector3 v2 = chunk.vertices[chunk.triangles[triangleIndex + 2]];
        return Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
    }

    private float calculerAngleAuVertex(Chunk chunk, int triangleIndex, int vertex)
    {
        int i0 = chunk.triangles[triangleIndex];
        int i1 = chunk.triangles[triangleIndex + 1];
        int i2 = chunk.triangles[triangleIndex + 2];

        int autre1, autre2;
        if (vertex == i0) { autre1 = i1; autre2 = i2; }
        else if (vertex == i1) { autre1 = i0; autre2 = i2; }
        else { autre1 = i0; autre2 = i1; }

        Vector3 a = chunk.vertices[autre1] - chunk.vertices[vertex];
        Vector3 b = chunk.vertices[autre2] - chunk.vertices[vertex];
        if (a.sqrMagnitude < 0.000001f || b.sqrMagnitude < 0.000001f)
            return 0f;
        return Vector3.Angle(a, b);
    }

    private Vector3 sommeNormales(Chunk chunk, int vertex)
    {
        Vector3 somme = Vector3.zero;
        foreach (int t in chunk.trianglesParVertex[vertex])
        {
            Vector3 normale = calculerNormaleTriangle(chunk, t);
            if (modeNormale == ModeNormale.Surface)
                normale *= calculerSurfaceTriangle(chunk, t);
            else if (modeNormale == ModeNormale.Angle)
                normale *= calculerAngleAuVertex(chunk, t, vertex);
            somme += normale;
        }
        return somme;
    }

    private void recalculerNormalesChunk(Chunk chunk)
    {
        recalculerNormalesZone(chunk, 0, chunk.res - 1, 0, chunk.res - 1);
    }

    // Recalcule les normales dans une boite de la grille (sculpture : seulement la zone touchee).
    // Un vertex de bordure somme aussi les triangles de ses jumeaux, puis leur recopie la normale.
    private void recalculerNormalesZone(Chunk chunk, int x0, int x1, int z0, int z1)
    {
        int last = chunk.res - 1;
        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                int i = z * chunk.res + x;
                Vector3 somme = sommeNormales(chunk, i);
                bool bord = (x == 0 || x == last || z == 0 || z == last);
                if (bord)
                {
                    trouverJumeaux(chunk, x, z);
                    for (int k = 0; k < p_membresTmp.Count; k++)
                        somme += sommeNormales(p_membresTmp[k], p_indicesTmp[k]);
                }

                Vector3 normale = somme.sqrMagnitude < 0.000001f ? Vector3.up : somme.normalized;
                chunk.normals[i] = normale;

                if (bord)
                    for (int k = 0; k < p_membresTmp.Count; k++)
                        p_membresTmp[k].normals[p_indicesTmp[k]] = normale;
            }
        }
    }

    // Mode Sequentiel : boucle C# sur tous les chunks. Sinon : un NormalesVertexJob (Burst) par chunk,
    // puis correction des bords (le job ne voit que les triangles d'un seul chunk).
    private void recalculerToutesLesNormales()
    {
        if (p_chunks.Count == 0)
            return;

        if (modeExecution == ModeExecution.Sequentiel || !p_voisinsNativesPrets)
        {
            foreach (Chunk chunk in p_chunks.Values)
                recalculerNormalesChunk(chunk);
            return;
        }

        foreach (Chunk chunk in p_chunks.Values)
            normalesParJob(chunk);

        if (p_chunks.Count > 1)
            foreach (Chunk chunk in p_chunks.Values)
                recalculerNormalesBords(chunk);
    }


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

    // APPLICATION DU MESH

    //private void appliquerMesh()
    //{
    //    foreach (var chunk in p_chunks.Values)
    //    {
    //        appliquerMeshChunk(chunk);
    //    }

    //}

    // DEFORMATION PAR FONCTION

    private void appliquerColline(Vector3[] v, ModeExecution mode, float centreX, float centreZ)
    {
        float sigma = Mathf.Max(0.01f, largeurColline);
        if (mode == ModeExecution.Sequentiel)
        {
            for (int index = 0; index < v.Length; index++)
            {
                Vector3 p = v[index];
                float dx = p.x - centreX;
                float dz = p.z - centreZ;
                p.y = hauteurColline * Mathf.Exp(-(dx * dx + dz * dz) / (2f * sigma * sigma));
                v[index] = p;
            }
            return;
        }
   

        NativeArray<Vector3> natif = new NativeArray<Vector3>(v, Allocator.TempJob);
        if (mode == ModeExecution.Jobs)
            new CollineTerrainJob { vertices = natif, centreX = centreX, centreZ = centreZ, sigma = sigma, hauteur = hauteurColline }
                .Schedule(natif.Length, lotJobs).Complete();
        else
            new CollineTerrainBurstJob { vertices = natif, centreX = centreX, centreZ = centreZ, sigma = sigma, hauteur = hauteurColline }
                .Schedule(natif.Length, lotJobs).Complete();
        natif.CopyTo(v);
        natif.Dispose();
    }


// ---- 3/4 PERLIN ------------------------------------------------------
    private void appliquerPerlin(Vector3[] v, ModeExecution mode)
    {
        if (mode == ModeExecution.Sequentiel)
        {
            for (int index = 0; index < v.Length; index++)
            {
                Vector3 p = v[index];
                Unity.Mathematics.float2 q = new Unity.Mathematics.float2(
                    p.x * echellePerlin + p_seedPerlin, p.z * echellePerlin + p_seedPerlin);
                p.y = Unity.Mathematics.noise.cnoise(q) * hauteurPerlin;
                v[index] = p;
            }
            return;
        }

        NativeArray<Vector3> natif = new NativeArray<Vector3>(v, Allocator.TempJob);
        if (mode == ModeExecution.Jobs)
            new PerlinTerrainJob { vertices = natif, echelle = echellePerlin, seed = p_seedPerlin, hauteur = hauteurPerlin }
                .Schedule(natif.Length, lotJobs).Complete();
        else
            new PerlinTerrainBurstJob { vertices = natif, echelle = echellePerlin, seed = p_seedPerlin, hauteur = hauteurPerlin }
                .Schedule(natif.Length, lotJobs).Complete();
        natif.CopyTo(v);
        natif.Dispose();
    }

    // ---- 4/4 HEIGHTMAP (interpolation bilineaire des 4 pixels voisins) ----
    // uDebut/uEchelle/vDebut/vEchelle : portion de l'image couverte par ce tableau
    // (0,1,0,1 = image entiere ; sinon part du chunk dans la heightmap etalee sur tout le terrain).
    private void appliquerHeightMap(Vector3[] v, Color[] pixels, int largeur, int hauteur, ModeExecution mode,
                                    float uDebut = 0f, float uEchelle = 1f, float vDebut = 0f, float vEchelle = 1f)
    {
        int res = chunkBase.res;

        if (mode == ModeExecution.Sequentiel)
        {
            for (int index = 0; index < v.Length; index++)
            {
                float u = uDebut + ((float)(index % res) / (res - 1)) * uEchelle;
                float w = vDebut + ((float)(index / res) / (res - 1)) * vEchelle;
                float px = u * (largeur - 1);
                float py = w * (hauteur - 1);
                int x0 = (int)px;
                int y0 = (int)py;
                int x1 = Mathf.Min(x0 + 1, largeur - 1);
                int y1 = Mathf.Min(y0 + 1, hauteur - 1);
                float tx = px - x0;
                float ty = py - y0;

                Color cx0 = Color.Lerp(pixels[y0 * largeur + x0], pixels[y0 * largeur + x1], tx);
                Color cx1 = Color.Lerp(pixels[y1 * largeur + x0], pixels[y1 * largeur + x1], tx);
                Color c = Color.Lerp(cx0, cx1, ty);

                Vector3 p = v[index];
                p.y = c.grayscale * hauteurTexture;
                v[index] = p;
            }
            return;
        }

        NativeArray<Color> pixelsNatif = new NativeArray<Color>(pixels, Allocator.TempJob);
        NativeArray<Vector3> natif = new NativeArray<Vector3>(v, Allocator.TempJob);
        if (mode == ModeExecution.Jobs)
            new TextureTerrainJob
            {
                pixels = pixelsNatif,
                vertices = natif,
                largeurImage = largeur,
                hauteurImage = hauteur,
                resolution = res,
                hauteurMax = hauteurTexture,
                uDebut = uDebut,
                uEchelle = uEchelle,
                vDebut = vDebut,
                vEchelle = vEchelle
            }.Schedule(natif.Length, lotJobs).Complete();
        else
            new TextureTerrainBurstJob
            {
                pixels = pixelsNatif,
                vertices = natif,
                largeurImage = largeur,
                hauteurImage = hauteur,
                resolution = res,
                hauteurMax = hauteurTexture,
                uDebut = uDebut,
                uEchelle = uEchelle,
                vDebut = vDebut,
                vEchelle = vEchelle
            }.Schedule(natif.Length, lotJobs).Complete();
        natif.CopyTo(v);
        natif.Dispose();
        pixelsNatif.Dispose();
    }

    // Applique un traitement a chaque chunk en coordonnees GLOBALES (x/z decales de la position
    // du chunk) pour que les motifs se raccordent d'un chunk a l'autre. Seule la hauteur y est recopiee.
    private void deformerChunks(System.Action<Chunk, Vector3[]> traitement)
    {
        foreach (Chunk chunk in p_chunks.Values)
        {
            Vector3 decalage = decalageChunk(chunk);
            Vector3[] tampon = new Vector3[chunk.vertices.Length];
            for (int i = 0; i < tampon.Length; i++)
            {
                Vector3 p = chunk.vertices[i];
                tampon[i] = new Vector3(p.x + decalage.x, p.y, p.z + decalage.z);
            }

            traitement(chunk, tampon);

            for (int i = 0; i < tampon.Length; i++)
                chunk.vertices[i].y = tampon[i].y;
        }
    }

    // Jumeaux de bord identiques, normales, puis envoi des meshes
    private void finaliserDeformation()
    {
        synchroniserJumeaux(new List<Chunk>(p_chunks.Values));
        recalculerToutesLesNormales();
        appliquerMesh();
    }

    // ---- F2 : deformation par fonction (tous les chunks) ------------------
    private void appliquerDeformation_Fonction()
    {
        if (p_chunks.Count == 0)
            return;

        switch (typeFonction)
        {
            case TypeFonction.Sinusoide:
                deformerChunks((c, v) => appliquerSinusoide(v, modeExecution));
                break;

            case TypeFonction.Collines:
                Vector3 centre = p_pointPickingDisponible
                    ? transform.InverseTransformPoint(p_dernierPointPicking)
                    : Vector3.zero;
                deformerChunks((c, v) => appliquerColline(v, modeExecution, centre.x, centre.z));
                break;

            case TypeFonction.Perlin:
                p_seedPerlin = Random.Range(0f, 10000f);
                deformerChunks((c, v) => appliquerPerlin(v, modeExecution));
                break;
        }

        finaliserDeformation();
    }


    // SINUSOIDE
    private void appliquerSinusoide(Vector3[] v, ModeExecution mode)
    {
        if (v == null || v.Length == 0)
            return;

        NativeArray<Vector3> verticesNative =
            new NativeArray<Vector3>(v, Allocator.TempJob);

        SinusoideTerrainBurstJob job = new SinusoideTerrainBurstJob
        {
            vertices = verticesNative,
            frequence = 2f * Mathf.PI / dimension,
            hauteur = hauteurSinusoide
        };

        JobHandle handle = job.Schedule(verticesNative.Length, 64);
        handle.Complete();

        verticesNative.CopyTo(v);
        verticesNative.Dispose();
    }

    private bool lirePixelsHeightMap(
    out Color[] pixels,
    out Texture2D texture)
    {
        pixels = null;
        texture = null;

        if (p_chunks.Count == 0)
            return false;

        if (textures == null || textures.Count == 0)
        {
            Debug.LogWarning(
                "Aucune HeightMap n'est renseignee."
            );
            return false;
        }

        numTexture =
            Mathf.Clamp(
                numTexture,
                0,
                textures.Count - 1
            );

        texture = textures[numTexture];

        if (texture == null)
        {
            Debug.LogWarning(
                "La HeightMap selectionnee est vide."
            );
            return false;
        }

        try
        {
            pixels = texture.GetPixels();
            return true;
        }
        catch
        {
            Debug.LogError(
                "Impossible de lire la HeightMap : " +
                "activer Read/Write dans l'import de la texture."
            );

            return false;
        }
    }


    private void appliquerDeformation_Texture()
    {
        if (!lirePixelsHeightMap(
            out Color[] pixels,
            out Texture2D texture))
            return;

        int largeurGrille =
            gridMax.x - gridMin.x + 1;

        int hauteurGrille =
            gridMax.y - gridMin.y + 1;

        deformerChunks(
            (c, v) =>
                appliquerHeightMap(
                    v,
                    pixels,
                    texture.width,
                    texture.height,
                    modeExecution,
                    (c.coord.x - gridMin.x)
                        / (float)largeurGrille,
                    1f / largeurGrille,
                    (c.coord.y - gridMin.y)
                        / (float)hauteurGrille,
                    1f / hauteurGrille
                )
        );

        finaliserDeformation();
    }

    

    // TP5 : DEFORMATION PAR HEIGHTMAP EN ASYNCHRONE (F4)

    private void lancerDeformationTextureAsync()
    {
        if (p_asyncEnCours || p_chunks.Count == 0 || !p_voisinsNativesPrets)
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
        int largeur = gridMax.x - gridMin.x + 1;
        int hauteur = gridMax.y - gridMin.y + 1;

        NativeList<JobHandle> handles = new NativeList<JobHandle>(Allocator.Temp);
        p_asyncChunks.Clear();

        foreach (Chunk chunk in p_chunks.Values)
        {
            AsyncChunk ac = new AsyncChunk { chunk = chunk };
            int n = chunk.vertices.Length;
            ac.vertices = new NativeArray<Vector3>(chunk.vertices, Allocator.Persistent);
            ac.normales = new NativeArray<Vector3>(n, Allocator.Persistent);

            JobHandle handleTexture = new TextureTerrainBurstJob
            {
                pixels = p_asyncPixels,
                vertices = ac.vertices,
                largeurImage = texture.width,
                hauteurImage = texture.height,
                resolution = chunk.res,
                hauteurMax = hauteurTexture,
                uDebut = (chunk.coord.x - gridMin.x) / (float)largeur,
                uEchelle = 1f / largeur,
                vDebut = (chunk.coord.y - gridMin.y) / (float)hauteur,
                vEchelle = 1f / hauteur
            }.Schedule(n, lotJobs);

            // dependance : les normales attendent la fin du job texture
            JobHandle h2 = new NormalesVertexJob
            {
                vertices = ac.vertices,
                triangles = p_trianglesNative,
                debut = p_debutVoisinsNative,
                liste = p_listeVoisinsNative,
                mode = (int)modeNormale,
                normales = ac.normales
            }.Schedule(n, lotJobs, handleTexture);

            handles.Add(h2);
            p_asyncChunks.Add(ac);
        }

        p_handleAsync = JobHandle.CombineDependencies(handles.AsArray());
        handles.Dispose();
        TextureTerrainBurstJob jobTexture = new TextureTerrainBurstJob
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

        p_handleAsync.Complete();                 // instantane : les jobs sont deja finis
        foreach (AsyncChunk ac in p_asyncChunks)
        {
            ac.vertices.CopyTo(ac.chunk.vertices);
            ac.normales.CopyTo(ac.chunk.normals);
        }
        libererAsync();
        p_asyncEnCours = false;

        // Les jobs n'ont vu qu'un chunk a la fois : on raccorde hauteurs et normales de bord
        synchroniserJumeaux(new List<Chunk>(p_chunks.Values));
        if (p_chunks.Count > 1)
            foreach (Chunk chunk in p_chunks.Values)
                recalculerNormalesBords(chunk);

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
        foreach (AsyncChunk ac in p_asyncChunks)
        {
            if (ac.vertices.IsCreated) ac.vertices.Dispose();
            if (ac.normales.IsCreated) ac.normales.Dispose();
        }
        p_asyncChunks.Clear();
        if (p_asyncPixels.IsCreated) p_asyncPixels.Dispose();
    }

    
    // --------------------------------------------------------------------
    // RECALCUL DE TOUTES LES NORMALES (job + Burst, repli sequentiel)
    // --------------------------------------------------------------------


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

    // MESURE DES PERFORMANCES DES 4 JOBS + NORMALES
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
                Unity.Mathematics.float2 p = new Unity.Mathematics.float2(
                    v.x * echellePerlin + seed,
                    v.z * echellePerlin + seed);
                float bruit = Unity.Mathematics.noise.cnoise(p);
                v.y = bruit * hauteurPerlin;
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
    private double mesurerSinusoideJobBurst()
    {
        if (p_vertices == null || p_vertices.Length == 0)
            return -1.0;

        System.Diagnostics.Stopwatch chrono = System.Diagnostics.Stopwatch.StartNew();
        float frequence = 2f * Mathf.PI / dimension;

        for (int rep = 0; rep < repetitionsMesure; rep++)
        {
            NativeArray<Vector3> verticesNative =
                new NativeArray<Vector3>(p_vertices, Allocator.TempJob);

            SinusoideTerrainBurstJob job = new SinusoideTerrainBurstJob
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

    private double mesurerCollineJobBurst()
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

            CollineTerrainBurstJob job = new CollineTerrainBurstJob
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

    private double mesurerTextureJobBurst()
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

            TextureTerrainBurstJob job = new TextureTerrainBurstJob
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

    // TP5/TP6 : MESURES COMPLEMENTAIRES

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

    private void etendreTerrain(Vector2Int direction)
    {
        if (p_chunks.Count == 0)
            return;

        if (direction.x != 0)
        {
            int x = (direction.x > 0) ? gridMax.x + 1 : gridMin.x - 1;
            for (int z = gridMin.y; z <= gridMax.y; z++)
                creerChunk(new Vector2Int(x, z));
            if (direction.x > 0) gridMax.x = x; else gridMin.x = x;
        }
        else
        {
            int z = (direction.y > 0) ? gridMax.y + 1 : gridMin.y - 1;
            for (int x = gridMin.x; x <= gridMax.x; x++)
                creerChunk(new Vector2Int(x, z));
            if (direction.y > 0) gridMax.y = z; else gridMin.y = z;
        }
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

    // F6 : LIMITES MEMOIRE / TEST DE CHARGE
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
    private void afficherResultatMesure4(
        string nom,
        double tempsSequentiel,
        double tempsJobs,
        double tempsJobsBurst)
    {
        string gainJobs = tempsJobs > 0.0 ? (tempsSequentiel / tempsJobs).ToString("F2") + " x" : "n/a";
        string gainBurst = tempsJobsBurst > 0.0 ? (tempsSequentiel / tempsJobsBurst).ToString("F2") + " x" : "n/a";

        Debug.Log(
            "\n--- " + nom + " ---\n" +
            "C# sequentiel : " + tempsSequentiel.ToString("F3") + " ms / repetition\n" +
            "Jobs seul     : " + tempsJobs.ToString("F3") + " ms / repetition\n" +
            "Jobs + Burst  : " + tempsJobsBurst.ToString("F3") + " ms / repetition\n" +
            "Gain Jobs     : " + gainJobs + "\n" +
            "Gain Burst    : " + gainBurst + "\n");

        p_derniersResultats.Add(nom + " : " + tempsSequentiel.ToString("F2") + " / " + tempsJobs.ToString("F2") + " / " + tempsJobsBurst.ToString("F2") + " ms");
        if (p_derniersResultats.Count > 4)
            p_derniersResultats.RemoveAt(0);
    }

    private void mesurerPerformances4Jobs()
    {
        if (p_vertices == null || p_vertices.Length == 0)
        {
            Debug.LogWarning(
                "Impossible de mesurer : le terrain n'est pas initialise.");
            return;
        }

        Debug.Log(
            "\n============================================================\n" +
            "TP5 - COMPARAISON DES 4 TRAITEMENTS\n" +
            "Jobs -> Jobs + Burst\n" +
            "Vertices : " + p_vertices.Length + "\n" +
            "Repetitions : " + repetitionsMesure + "\n" +
            "Lot Jobs : " + lotMesure + "\n" +
            "Burst actuellement : " + (burstEstActif() ? "ACTIF" : "INACTIF") + "\n" +
            "============================================================");

        double sinusoideSeq = mesurerSinusoideSequentiel();
        double sinusoideJob = mesurerSinusoideJob();
        double sinusoideBurst = mesurerSinusoideJobBurst();

        double collineSeq = mesurerCollineSequentiel();
        double collineJob = mesurerCollineJob();
        double collineBurst = mesurerCollineJobBurst();

        double perlinSeq = mesurerPerlinSequentiel();
        double perlinJob = mesurerPerlinJob();
        double perlinBurst = mesurerPerlinJobBurst();

        double textureSeq = mesurerTextureSequentiel();
        double textureJob = mesurerTextureJob();
        double textureBurst = mesurerTextureJobBurst();

        afficherResultatMesure4(
            "1/4 SINUSOIDE",
            sinusoideSeq,
            sinusoideJob,
            sinusoideBurst);

        afficherResultatMesure4(
            "2/4 COLLINE GAUSSIENNE",
            collineSeq,
            collineJob,
            collineBurst);

        afficherResultatMesure4(
            "3/4 PERLIN",
            perlinSeq,
            perlinJob,
            perlinBurst);

        afficherResultatMesure4(
            "4/4 HEIGHTMAP / TEXTURE",
            textureSeq,
            textureJob,
            textureBurst);

        // Mesures complementaires du TP5.
        mesurerNormales();
        mesurerReinitialisation();
        mesurerBakerVoisins();
        mesurerInfluenceLot();

        Debug.Log(
            "\n============================================================\n" +
            "FIN DES MESURES TP5 - 4 TRAITEMENTS\n" +
            "============================================================");
    }

    // UPDATE
    void Update()
    {
        gererJobAsync();
        gererMisesAJourDifferees();
        p_fps = Mathf.Lerp(p_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.05f);
        gererClavier();
        gererCamera();
        //gererPicking();
        gererSculptureTerrain();
        gererAffichageNormales();
        gererDoubleF11();
        mettreAJourUI();
    }

    // CLAVIER

    private void gererClavier()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            p_afficherAide = !p_afficherAide;
            if (p_panneauAide != null)
                p_panneauAide.SetActive(p_afficherAide);
        }

        // F2/F3 : fonctions / heightmaps
        bool f2 = Keyboard.current.f2Key.wasPressedThisFrame;
        bool f3 = Keyboard.current.f3Key.wasPressedThisFrame;

        if ((f2 || f3) && p_chunks.Count > 1)
        {
            Debug.LogWarning("Fonctions / HeightMaps réservées au terrain non étendu.");
        }
        else
        {
            if (f2)
            {
                choixModeDeformation = ChoixModeDeformation.Fonction;
                typeFonction = (TypeFonction)(((int)typeFonction + 1) % 3);
                appliquerDeformation_Fonction();
            }
            if (f3)
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

        // D : changer de metrique de distance
        if (Keyboard.current.dKey.wasPressedThisFrame)
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

        // C : Surlugnage des chunks
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            StartCoroutine(surligneChunks());
        }

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

    // PICKING

    private void gererPicking()
    {
        if (Mouse.current == null)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (!effectuerPicking(out RaycastHit hit))
            return;

        p_dernierPointPicking = hit.point;
        p_pointPickingDisponible = true;

        if (choixModeDeformation == ChoixModeDeformation.Fonction &&
            typeFonction == TypeFonction.Collines)
        {
            Chunk chunk = chunkBase;

            if (chunk != null)
            {
                Vector3 pointLocal =
                    chunk.go.transform.InverseTransformPoint(hit.point);

                appliquerColline(
                    chunk.vertices,
                    modeExecution,
                    pointLocal.x,
                    pointLocal.z
                );

                recalculerToutesLesNormales();
                appliquerMesh();
            }
        }
    }

    

    // CAMERA
    private void gererCamera()
    {
        if (p_cam == null || Keyboard.current == null)
            return;

        float deltaTemps = vitesseCamera * Time.deltaTime;
        Vector3 direction = Vector3.zero;

        // CAMERA : AZQS
        if (Keyboard.current.wKey.isPressed)
            direction += p_cam.transform.forward;

        if (Keyboard.current.sKey.isPressed)
            direction -= p_cam.transform.forward;

        if (Keyboard.current.qKey.isPressed)
            direction -= p_cam.transform.right;

        if (Keyboard.current.eKey.isPressed)
            direction += p_cam.transform.right;

        if (Keyboard.current.pageUpKey.isPressed)
            direction += Vector3.up;

        if (Keyboard.current.pageDownKey.isPressed)
            direction -= Vector3.up;

        if (direction.sqrMagnitude > 0.001f)
            p_cam.transform.position += direction.normalized * deltaTemps;

        // SOURIS DROITE : rotation de la camera.
        if (Mouse.current != null &&
            Mouse.current.middleButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();

            float yaw = delta.x * sensibiliteSourisCamera;
            float pitch = -delta.y * sensibiliteSourisCamera;

            p_cam.transform.Rotate(
                Vector3.up,
                yaw,
                Space.World);

            p_cameraPitch = Mathf.Clamp(
                p_cameraPitch + pitch,
                -89f,
                89f);

            Vector3 angles = p_cam.transform.localEulerAngles;
            angles.x = p_cameraPitch;
            p_cam.transform.localEulerAngles = angles;
        }

        // R : rotation continue du terrain.
        if (Keyboard.current.rKey.isPressed)
        {
            transform.Rotate(
                Vector3.up,
                vitesseRotationTerrain * Time.deltaTime,
                Space.World);
        }
    }
    // AFFICHAGE DES NORMALES

    private void gererAffichageNormales()
    {
        if (p_tempsAffichageNormales <= 0f)
            return;

        foreach (Chunk chunk in p_chunks.Values)
        {
            Transform t = chunk.go.transform;
            int pas = Mathf.Max(1, chunk.vertices.Length / 1000);   // evite des milliers de lignes par frame

            for (int i = 0; i < chunk.vertices.Length; i += pas)
            {
                Vector3 origine = t.TransformPoint(chunk.vertices[i]);

                if (p_modeAffichageNormales == 3)
                {
                    Debug.DrawLine(origine, origine + calculerNormaleEclairage(chunk, i) * 2f, Color.green);
                    Debug.DrawLine(origine, origine + calculerNormaleOrientation(chunk, i) * 2f, Color.red);
                    continue;
                }

                Vector3 direction =
                    p_modeAffichageNormales == 0 ? t.TransformDirection(chunk.normals[i]) :
                    p_modeAffichageNormales == 1 ? calculerNormaleEclairage(chunk, i) :
                                                   calculerNormaleOrientation(chunk, i);
                Debug.DrawLine(origine, origine + direction * 2f, Color.yellow);
            }
        }
    }

    // Normale d'eclairage : moyenne des normales des 3 vertices de chaque triangle rattache
    private Vector3 calculerNormaleEclairage(Chunk chunk, int vertex)

    {
        List<int> triangles = chunk.trianglesParVertex[vertex];

        Vector3 resultat = Vector3.zero;
        int nombre = 0;

        foreach (int tri in triangles)
        {
            resultat +=
                chunk.normals[chunk.triangles[tri]] +
                chunk.normals[chunk.triangles[tri + 1]] +
                chunk.normals[chunk.triangles[tri + 2]];

            nombre += 3;
        }

        if (nombre == 0)
            return chunk.go.transform.up;

        return chunk.go.transform.TransformDirection(
            (resultat / nombre).normalized
        );
    }

    // Normale d'orientation : produit vectoriel V01 ^ V02 du premier triangle rattache
    private Vector3 calculerNormaleOrientation(Chunk chunk, int vertex)
    {
        List<int> triangles = chunk.trianglesParVertex[vertex];
        if (triangles == null || triangles.Count == 0)
            return chunk.go.transform.up;

        return chunk.go.transform.TransformDirection(calculerNormaleTriangle(chunk, triangles[0]));
    }

    // --------------------------------------------------------------------
    // MEMOIRE
    // --------------------------------------------------------------------


    //private Vector3 calculerNormaleTriangle(int triangleIndex) => calculerNormaleTriangle(chunkBase, triangleIndex);

    
    // GUI
    private void OnGUI()
    {
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
        GUILayout.Label("F2 : Fonction suivante   |   F3 : HeightMap suivante");
        GUILayout.Label("F4 : HeightMap asynchrone (plus-value TP5)");
        GUILayout.Label("F5 : Mesurer les 4 traitements : C# / Jobs / Jobs+Burst");
        GUILayout.Label("F6 : Test memoire / charge");
        GUILayout.Label("F10 : Afficher les normales   |   F11 x2 : Terrain plat");
        GUILayout.Label("F12 : Changer le calcul des normales");
        GUILayout.Label("D : Changer de distance (TP4)");
        GUILayout.Label("Z / S : Avancer / reculer   |   Q / E : Gauche / droite");
        GUILayout.Label("PageUp / PageDown : Monter / descendre");
        GUILayout.Label("Clic milieu + souris : Tourner la camera   |   R : Tourner le terrain");
        GUILayout.Label("Fleches : Etendre le terrain avec les chunks (TP4)");
        GUILayout.Label("C : Surligner les chunks pendant 3 secondes");

        GUILayout.Space(10);
        GUILayout.Label("SCULPTURE INTERACTIVE (EXERCICE 1)");
        GUILayout.Label($"Pattern Actif : {p_indexPatternCourant}   |   Intensite Max : {intensiteMaxDeformation}   |   Rayon : {rayonDeformation}");
        GUILayout.Label("Clic Gauche / Droit : Sculpter (Elevation / Depression)");
        GUILayout.Label("Molette + SHIFT / CTRL / ALT : Varier intensite / rayon / pattern");

        GUILayout.Space(10);
        GUILayout.Label("EXERCICE 2 - DISTANCES");
        GUILayout.Label("D : Euclidienne / Euclidienne carree / Manhattan / Chebyshev");
        GUILayout.Label($"Espace : Visualiser le voisinage   |   Voisins : {p_nombreVoisins}");

        GUILayout.Space(10);
        GUILayout.Label("PARAMETRES LOD :");

        // Affichage si le LOD est actif ou pas
        LODGroup lodGroup = chunkBase != null ? chunkBase.go.GetComponent<LODGroup>() : null;
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

    // MEMOIRE

private string libelleModeExecution()
    {
        switch (choixModeDeformation)
        {
            default: return p_burstActif ? "Jobs + Burst" : "Jobs + Burst (Burst INACTIF)";
        }
    }

    private static string nomTouche(UnityEngine.InputSystem.Controls.KeyControl k)
    {
        return k != null ? k.displayName : "?";
    }

    private string texteInteractions()
    {
        Keyboard kb = Keyboard.current;
        string avancer = kb != null ? nomTouche(kb.wKey) : "W";
        string reculer = kb != null ? nomTouche(kb.sKey) : "S";
        string gauche = kb != null ? nomTouche(kb.qKey) : "Q";
        string droite = kb != null ? nomTouche(kb.eKey) : "E";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>INTERACTIONS</b>");
        sb.AppendLine("F1 : afficher / masquer cette fenetre");
        sb.AppendLine("F2 : fonction suivante");
        sb.AppendLine("F3 : HeightMap suivante");
        sb.AppendLine("F4 : HeightMap asynchrone");
        sb.AppendLine("F5 : comparer C# / Jobs / Jobs+Burst (console)");
        sb.AppendLine("F6 : memoire + test de charge (console)");
        sb.AppendLine("F10 : afficher les normales");
        sb.AppendLine("F11 x2 : terrain plat");
        sb.AppendLine("F12 : calcul des normales");
        sb.AppendLine("D : changer de distance");
        sb.AppendLine("");
        sb.AppendLine("<b>CAMERA / TERRAIN</b>");
        sb.AppendLine(avancer + " / " + reculer + " : avancer / reculer   |   " + gauche + " / " + droite + " : gauche / droite");
        sb.AppendLine("PageUp / PageDown : monter / descendre");
        sb.AppendLine("Molette seule : avancer / reculer");
        sb.AppendLine("Clic milieu + souris : orienter la camera");
        sb.AppendLine("R (maintenu) : tourner le terrain");
        sb.AppendLine("Fleches : etendre le terrain");
        sb.AppendLine("C : un materiau par chunk pendant 3 s");
        sb.AppendLine("");
        sb.AppendLine("<b>SCULPTURE</b>");
        sb.AppendLine("Clic gauche : elever   |   Clic droit : creuser");
        sb.AppendLine("Molette + Maj : intensite");
        sb.AppendLine("Molette + Ctrl : rayon");
        sb.AppendLine("Molette + Alt : pattern suivant / precedent");
        return sb.ToString();
    }

    private Text creerTexte(Transform parent, string nom, Font police, int taille,
                            Vector2 ancreMin, Vector2 ancreMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.font = police;
        t.fontSize = taille;
        t.color = Color.white;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.alignment = TextAnchor.UpperLeft;
        RectTransform rt = t.rectTransform;
        rt.anchorMin = ancreMin;
        rt.anchorMax = ancreMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return t;
    }

    private void creerUI()
    {
        Font police = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (police == null)
            police = Font.CreateDynamicFontFromOSFont("Arial", 16);

        GameObject racine = new GameObject("UI_Terrain");
        racine.transform.SetParent(transform, false);
        Canvas canvas = racine.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = racine.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        p_texteFps = creerTexte(racine.transform, "FPS", police, 22,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -35f), new Vector2(400f, -5f));

        p_panneauAide = new GameObject("FenetreF1");
        p_panneauAide.transform.SetParent(racine.transform, false);
        Image fond = p_panneauAide.AddComponent<Image>();
        fond.color = new Color(0f, 0f, 0f, 0.8f);
        fond.raycastTarget = false;
        RectTransform rp = fond.rectTransform;
        rp.anchorMin = new Vector2(0.5f, 0.5f);
        rp.anchorMax = new Vector2(0.5f, 0.5f);
        rp.pivot = new Vector2(0.5f, 0.5f);
        rp.sizeDelta = new Vector2(1600f, 960f);

        p_texteGauche = creerTexte(p_panneauAide.transform, "Etat", police, 17,
            new Vector2(0f, 0f), new Vector2(0.52f, 1f), new Vector2(20f, 15f), new Vector2(-10f, -15f));
        p_texteDroite = creerTexte(p_panneauAide.transform, "Interactions", police, 17,
            new Vector2(0.52f, 0f), new Vector2(1f, 1f), new Vector2(10f, 15f), new Vector2(-20f, -15f));

        p_texteDroite.text = texteInteractions();
        p_panneauAide.SetActive(false);
    }

    private void mettreAJourUI()
    {
        if (p_texteFps == null || Time.unscaledTime < p_prochaineMajUI)
            return;
        p_prochaineMajUI = Time.unscaledTime + 0.25f;

        p_texteFps.text = "FPS : " + p_fps.ToString("F0") + "   |   " + libelleModeExecution();

        if (!p_panneauAide.activeSelf || chunkBase == null)
            return;

        StringBuilder sb = p_sb;
        sb.Clear();

        long totVertices = 0, totTriangles = 0, totMemoire = 0;
        foreach (Chunk c in p_chunks.Values)
        {
            totVertices += c.vertices.Length;
            totTriangles += c.triangles.Length / 3;
            totMemoire += (c.vertices.Length * 12L + c.normals.Length * 12L + c.uv.Length * 8L + c.triangles.Length * 4L) / 1024L;
        }

        sb.AppendLine("<b>INFORMATIONS DU MAILLAGE (chunk 0,0)</b>");
        sb.AppendLine("Vertices : " + p_vertices.Length + "   |   Triangles : " + p_triangles.Length / 3);
        sb.AppendLine("Resolution : " + chunkBase.res + " x " + chunkBase.res + "   |   Memoire : " + calculerMemoireMesh() + " Ko");
        sb.AppendLine("");
        sb.AppendLine("<b>CHUNKS</b>");
        sb.AppendLine("Grille : " + (gridMax.x - gridMin.x + 1) + " x " + (gridMax.y - gridMin.y + 1) + "   |   Chunks : " + p_chunks.Count);
        sb.AppendLine("Total : " + totVertices + " vertices, " + totTriangles + " triangles, " + totMemoire + " Ko");
        sb.AppendLine("");
        sb.AppendLine("<b>PARAMETRES EN COURS</b>");
        sb.AppendLine("Mode : " + choixModeDeformation + "   |   Fonction : " + typeFonction + "   |   HeightMap n° " + numTexture);
        sb.AppendLine("Normales : " + modeNormale + "   |   Distance : " + distanceUtilisee);
        sb.AppendLine("Pattern n° " + p_indexPatternCourant + " | Intensite : " + intensiteMaxDeformation + " | Rayon : " + rayonDeformation);

        if (p_derniersResultats.Count > 0)
        {
            sb.AppendLine("");
            sb.AppendLine("<b>DERNIERES MESURES F5</b>");
            foreach (string ligne in p_derniersResultats)
                sb.AppendLine(ligne);
        }

        p_texteGauche.text = sb.ToString();
    }


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

    // SCULPTURE DU TERRAIN

    private void gererSculptureTerrain()
    {
        if (Mouse.current == null)
            return;

        if (Keyboard.current == null)
            return;

        if (p_asyncEnCours)
            return;

        // MOLETTE

        float scrollY =
            Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scrollY) > 0.01f)
        {
            Debug.Log("MOLETTE = " + scrollY);

            // SHIFT + MOLETTE = INTENSITE

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

            // CTRL + MOLETTE = RAYON

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


            // ALT + MOLETTE = PATTERN

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

        // VERIFICATION DU PATTERN

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

        // CLIC / ESPACE

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
                    p_dernierPointPicking = hit.point;
                    p_pointPickingDisponible = true;

                    // TP3 : en mode Collines, le clic gauche place
                    // une colline gaussienne sous le pointeur.
                    if (choixModeDeformation == ChoixModeDeformation.Fonction &&
                        typeFonction == TypeFonction.Collines &&
                        clicGauche)
                    {
                        Chunk chunk = chunkBase;

                        if (chunk != null)
                        {
                            Vector3 pointLocal =
                                chunk.go.transform.InverseTransformPoint(hit.point);

                            appliquerColline(
                                chunk.vertices,
                                modeExecution,
                                pointLocal.x,
                                pointLocal.z
                            );
                        }
                        recalculerToutesLesNormales();
                        appliquerMesh();
                    }
                    else
                    {
                        // TP4 : sculpture par pattern.
                        appliquerPatternDeformation(
                            hit.point,
                            clicGauche
                        );
                    }
                }
            }
        }
    }
    // EXERCICE 2 : CALCUL DES DISTANCES
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

    // PATTERN DE DEFORMATION
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


    private void synchroniserJumeaux(List<Chunk> liste)
    {
        foreach (Chunk chunk in liste)
        {
            int last = chunk.res - 1;
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
        if (p_membresTmp.Count == 0)
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

    // PREVISUALISATION DE LA DEFORMATION
    private void previsualiserDeformation(Vector3 pointMonde)
    {
        if (p_chunks.Count == 0)
            return;

        p_nombreVoisins = 0;

        // Meme selection de chunks que pour la sculpture
        List<Chunk> touches = chunkTouches(pointMonde);

        foreach (Chunk chunk in touches)
        {
            // Centre exprime dans le repere local de CE chunk
            Vector3 centreLocal = chunk.go.transform.InverseTransformPoint(pointMonde);

            for (int i = 0; i < chunk.vertices.Length; i++)
            {
                bool dansRayon;

                calculerDistanceNormalisee(
                    chunk.vertices[i],
                    centreLocal,
                    out dansRayon
                );

                if (!dansRayon)
                    continue;

                Vector3 positionMondeVertex =
                    chunk.go.transform.TransformPoint(chunk.vertices[i]);

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
