using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class CreationSimpleTerrain : MonoBehaviour
{
    [Header("Paramètres du terrain")]

    [Range(1, 2000)]
    public float dimension = 100;

    [Range(1, 12)]
    [Tooltip("La résolution vaut 2^n")]
    public int puissance2Resolution = 4;

    public bool CentrerPivot = true;

    [HideInInspector]
    public ushort resolution;


    public enum ChoixModeDeformation
    {
        Fonction,
        Texture
    }

    [Header("Mode de génération")]
    public ChoixModeDeformation choixModeDeformation;


    public enum TypeFonction
    {
        Sinusoide,
        Collines,
        Perlin
    }

    public TypeFonction typeFonction;


    [Header("HeightMaps")]

    public int numTexture;

    public List<Texture2D> textures;


    [Header("Paramètres des fonctions")]

    public float hauteurSinusoide = 5f;

    public float hauteurColline = 10f;

    public float largeurColline = 15f;

    public float hauteurPerlin = 10f;

    public float echellePerlin = 0.05f;

    public float hauteurTexture = 20f;

    // Création des mesh et vertices pour le LOD
    private Mesh meshLOD0, meshLOD1, meshLOD2;

    private Vector3[] vertices0, vertices1, vertices2;

    private Vector3[] normales0, normales1, normales2;

    // Matrice de correspondance (Indice LOD1 -> Indice LOD0)
    private int[] mapLOD1to0;

    private int[] mapLOD2to0;


    //[Header("Normales")]
    public enum ModeNormale
    {
        Basique,
        Surface,
        Angle
    }

    public ModeNormale modeNormale = ModeNormale.Basique;


    [Header("Caméra")]

    public float vitesseCamera = 15f;

    public float vitesseRotationCamera = 80f;

    public float vitesseRotationTerrain = 50f;

    // --- NOUVEAUX PARAMÈTRES POUR L'EXERCICE 1 ---
    [Header("Sculpture Interactive (Exercice 1)")]
    [Tooltip("Ajoutez des courbes allant de X=0 à X=1, et Y=0 à Y=1")]
    public AnimationCurve[] patternsDeformation;
    public float rayonDeformation = 15f;
    public float intensiteMaxDeformation = 10f;
    private int p_indexPatternCourant = 0;
    private float p_cooldownScroll = 0f; // Evite les changement de pattern trop rapides lors du scroll de la souris


    // --------------------------------------------------------------------
    // Données internes
    // --------------------------------------------------------------------

    private uint p_dimVertices;
    private uint p_dimTriangles;

    private MeshCollider p_meshCollider;
    private MeshFilter p_meshFilter;
    private Mesh p_mesh;

    private Vector3[] p_vertices;
    private Vector3[] p_normals;
    private Vector2[] p_uv;
    private int[] p_triangles;

    private Camera p_cam;

    private LayerMask maskPickingTerrain;

    private float p_dimInterVertices;

    // Liste des triangles attachés à chaque vertex.
    private List<int>[] p_trianglesParVertex;

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


    // --------------------------------------------------------------------
    // RESET
    // --------------------------------------------------------------------

    void Reset()
    {
        p_meshFilter = GetComponent<MeshFilter>();
        p_meshCollider = GetComponent<MeshCollider>();

        if (GetComponent<MeshRenderer>() == null)
            gameObject.AddComponent<MeshRenderer>();

        if (p_meshCollider == null)
            p_meshCollider = gameObject.AddComponent<MeshCollider>();

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
    }


    // --------------------------------------------------------------------
    // START
    // --------------------------------------------------------------------

    void Start()
    {
        if (puissance2Resolution >= 6)
        {
            initialiserLODGroup();
        }
        else
        {
            creerLeMeshTerrain();
        }

        

        // Terrain initial plat.
        //remettreTerrainPlat();
    }

    // --------------------------------------------------------------------
    // INITIALISATION DU GROUPE LOD
    // --------------------------------------------------------------------
    void initialiserLODGroup()
    {
        // Calcul des résolutions
        int resLOD0 = 1 <<puissance2Resolution;
        int resLOD2 = 16;
        int puissanceLOD1 = (puissance2Resolution - 4) / 2;
        int resLOD1 = 1 << puissanceLOD1;

        // Création de la hiérarchie LODGroup
        LODGroup lodGroup = gameObject.AddComponent<LODGroup>();
        LOD[] lods = new LOD[3];

        // Création des 3 enfants MeshRenderer
        Renderer[] renderer0 = new Renderer[] { creerEnfantsLOD("LOD_0", resLOD0, out meshLOD0, out vertices0, out normales0) };
        Renderer[] renderer1 = new Renderer[] { creerEnfantsLOD("LOD_1", resLOD1, out meshLOD1, out vertices1, out normales1) };
        Renderer[] renderer2 = new Renderer[] { creerEnfantsLOD("LOD_2", resLOD2, out meshLOD2, out vertices2, out normales2) };

        // Références vers LOD0 pour le picking et le calcul
        p_mesh = meshLOD0;
        p_vertices = vertices0;
        p_normals = normales0;

        // Ajustement des seuils de basculementde de distance
        lods[0] = new LOD(0.5f, renderer0);
        lods[1] = new LOD(0.15f, renderer1);
        lods[2] = new LOD(0.02f, renderer2);

        lodGroup.SetLODs(lods);
        lodGroup.RecalculateBounds();

        calculerMapping(resLOD0, resLOD1, out mapLOD1to0);
        calculerMapping(resLOD0, resLOD2, out mapLOD2to0);

    }

    // --------------------------------------------------------------------
    // CREATION DES ENFANTS DES LOD
    // --------------------------------------------------------------------
    Renderer creerEnfantsLOD(string nom, int res, out Mesh mesh, out Vector3[] vertices, out Vector3[] norms)
    {
        GameObject child = new GameObject(nom);
        child.transform.SetParent(this.transform, false);

        MeshFilter mf = child.AddComponent<MeshFilter>();
        MeshRenderer mr = child.AddComponent<MeshRenderer>();
        //mr.sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;

        MeshRenderer mainRenderer = GetComponent<MeshRenderer>();
        if(mainRenderer != null)
            mr.sharedMaterial = mainRenderer.sharedMaterial;

        // Générer inline du maillage selon la résolution passé en paramètre
        mesh = creerMeshGrille(res, out vertices, out norms, out Vector2[] uvs, out int[] triangles);
        mf.sharedMesh = mesh;

        // Si c't le LOD0, affecter son maillage au MeshCollider principal
        if (nom == "LOD_0" && p_meshCollider != null)
            p_meshCollider.sharedMesh = mesh;

        return mr;
    }

    // --------------------------------------------------------------------
    // CALCUL DU MAPPING
    // --------------------------------------------------------------------
    void calculerMapping(int resHaut, int resBasse, out int[] map)
    {
        map = new int[resHaut * resBasse];
        int pas = (resHaut - 1) / (resBasse - 1);

        int idx = 0;
        for (int z = 0; z < resBasse; z++)
        {
            for (int x = 0; x < resBasse; x++)
            {
                int xHaut = pas * x;
                int zHaut = pas * z;
                map[idx++] = xHaut * resHaut * zHaut;

            }
        }
        
    }

    // --------------------------------------------------------------------
    // SYNCHRONISATION DES MODIFS DE HAUTEUR ET DE NORMAL
    // --------------------------------------------------------------------
    private void propagerLOD()
    {
        if (puissance2Resolution < 6)
            return;

        // Mise à jour du LOD1
        if(meshLOD1 != null && mapLOD1to0 != null)
        {
            for (int i = 0; i < vertices1.Length; i++)
            {
                int i0 = mapLOD1to0[i];
                vertices1[i].y = vertices0[i0].y;
            }
            meshLOD1.vertices = vertices1;
            meshLOD1.RecalculateNormals();
        }

        // Mise à jour du LOD2
        if(meshLOD2 != null && mapLOD2to0 != null)
        {
            for (int i = 0; i < vertices1.Length; i++)
            {
                int i0 = mapLOD2to0[i];
                vertices2[i].y = vertices0[i0].y;
            }
            meshLOD2.vertices = vertices2;
            meshLOD2.RecalculateNormals();
        }
    }


    // --------------------------------------------------------------------
    // CREATION DU MAILLAGE
    // --------------------------------------------------------------------

    private void creerLeMeshTerrain()
    {
        p_meshFilter = GetComponent<MeshFilter>();
        p_meshCollider = GetComponent<MeshCollider>();

        if (p_meshFilter == null)
            p_meshFilter = gameObject.AddComponent<MeshFilter>();

        if (p_meshCollider == null)
            p_meshCollider = gameObject.AddComponent<MeshCollider>();


        // ------------------------------------------------------------
        // Résolution
        // ------------------------------------------------------------

        int resolutionInt = 1 << puissance2Resolution;

        resolution = (ushort)Mathf.Clamp(
            resolutionInt,
            2,
            ushort.MaxValue
        );

        resolutionInt = resolution;


        // ------------------------------------------------------------
        // Nombre de vertices
        // ------------------------------------------------------------

        long nombreVertices = (long)resolutionInt * resolutionInt;

        long nombreTriangles =
            2L *
            (resolutionInt - 1) *
            (resolutionInt - 1);


        if (nombreVertices > int.MaxValue)
        {
            Debug.LogError("Le maillage contient trop de vertices.");
            return;
        }


        p_dimVertices = (uint)nombreVertices;
        p_dimTriangles = (uint)nombreTriangles;


        // ------------------------------------------------------------
        // Espacement
        // ------------------------------------------------------------

        p_dimInterVertices =
            dimension / (resolutionInt - 1);


        // ------------------------------------------------------------
        // Création des tableaux
        // ------------------------------------------------------------

        //p_vertices = new Vector3[nombreVertices];
        //p_normals = new Vector3[nombreVertices];
        //p_uv = new Vector2[nombreVertices];

        //p_triangles = new int[nombreTriangles * 3];


        // ------------------------------------------------------------
        // Création des vertices
        // ------------------------------------------------------------

        //float origine = CentrerPivot
        //    ? dimension * 0.5f
        //    : 0f;


        //for (int z = 0; z < resolutionInt; z++)
        //{
        //    for (int x = 0; x < resolutionInt; x++)
        //    {
        //        int index = z * resolutionInt + x;

        //        float px = x * p_dimInterVertices - origine;
        //        float pz = z * p_dimInterVertices - origine;

        //        p_vertices[index] =
        //            new Vector3(px, 0f, pz);


        //        // UV entre 0 et 1.
        //        float u =
        //            (float)x / (resolutionInt - 1);

        //        float v =
        //            (float)z / (resolutionInt - 1);

        //        p_uv[index] =
        //            new Vector2(u, v);

        //        p_normals[index] =
        //            Vector3.up;
        //    }
        //}


        // ------------------------------------------------------------
        // Création des triangles
        //
        // 3 -- 2
        // |  / |
        // 0 -- 1
        //
        // ------------------------------------------------------------

        //int triangleIndex = 0;

        //for (int z = 0; z < resolutionInt - 1; z++)
        //{
        //    for (int x = 0; x < resolutionInt - 1; x++)
        //    {
        //        int v0 = z * resolutionInt + x;
        //        int v1 = v0 + 1;
        //        int v2 = v0 + resolutionInt;
        //        int v3 = v2 + 1;


        //        // Triangle 1
        //        p_triangles[triangleIndex++] = v0;
        //        p_triangles[triangleIndex++] = v2;
        //        p_triangles[triangleIndex++] = v1;


        //        // Triangle 2
        //        p_triangles[triangleIndex++] = v1;
        //        p_triangles[triangleIndex++] = v2;
        //        p_triangles[triangleIndex++] = v3;
        //    }
        //}


        // ------------------------------------------------------------
        // Création du Mesh
        // ------------------------------------------------------------

        //if (p_mesh != null)
        //{
        //    Destroy(p_mesh);
        //}

        //p_mesh = new Mesh();

        //p_mesh.name = "TerrainProcedural";


        // ------------------------------------------------------------
        // IMPORTANT :
        // Plus de 65535 vertices => indices 32 bits.
        // ------------------------------------------------------------

        //if (nombreVertices > 65535)
        //{
        //    p_mesh.indexFormat = IndexFormat.UInt32;
        //}
        //else
        //{
        //    p_mesh.indexFormat = IndexFormat.UInt16;
        //}


        //p_mesh.vertices = p_vertices;
        //p_mesh.uv = p_uv;
        //p_mesh.triangles = p_triangles;
        //p_mesh.normals = p_normals;

        //p_mesh.RecalculateBounds();

        p_mesh = creerMeshGrille(resolutionInt, out p_vertices, out p_normals, out p_uv, out p_triangles);

        p_meshFilter.sharedMesh = p_mesh;

        p_meshCollider.sharedMesh = null;
        p_meshCollider.sharedMesh = p_mesh;


        // ------------------------------------------------------------
        // Baking des voisins
        // ------------------------------------------------------------

        bakerVoisins();


        Debug.Log(
            "Terrain créé : " +
            p_dimVertices +
            " vertices, " +
            p_dimTriangles +
            " triangles."
        );
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

    private void bakerVoisins()
    {
        p_trianglesParVertex =
            new List<int>[p_vertices.Length];

        if (p_triangles == null || p_vertices == null)
            return;

        for (int i = 0; i < p_trianglesParVertex.Length; i++)
        {
            p_trianglesParVertex[i] =
                new List<int>();
        }


        // Chaque triangle est associé à ses 3 vertices.
        for (int i = 0; i < p_triangles.Length; i += 3)
        {
            int a = p_triangles[i];
            int b = p_triangles[i + 1];
            int c = p_triangles[i + 2];


            p_trianglesParVertex[a].Add(i);
            p_trianglesParVertex[b].Add(i);
            p_trianglesParVertex[c].Add(i);
        }
    }


    // --------------------------------------------------------------------
    // TERRAIN PLAT
    // --------------------------------------------------------------------

    private void remettreTerrainPlat()
    {
        if (p_vertices == null)
            return;


        for (int i = 0; i < p_vertices.Length; i++)
        {
            p_vertices[i].y = 0f;
        }


        recalculerToutesLesNormales();

        appliquerMesh();
    }


    // --------------------------------------------------------------------
    // APPLICATION DU MESH
    // --------------------------------------------------------------------

    private void appliquerMesh()
    {
        if (p_mesh == null)
            return;

        p_mesh.vertices = p_vertices;
        p_mesh.normals = p_normals;
        p_mesh.uv = p_uv;

        p_mesh.RecalculateBounds();

        p_meshFilter.sharedMesh = p_mesh;

        p_meshCollider.sharedMesh = null;
        p_meshCollider.sharedMesh = p_mesh;

        // Propagation des modification aux sous maillages LOD1 et LOD2
        propagerLOD();
    }


    // --------------------------------------------------------------------
    // DEFORMATION PAR FONCTION
    // --------------------------------------------------------------------

    private void appliquerDeformation_Fonction()
    {
        if (p_vertices == null)
            return;


        // Nouvelle génération.
        remettreTerrainPlat();


        switch (typeFonction)
        {
            case TypeFonction.Sinusoide:

                appliquerSinusoide();

                break;


            case TypeFonction.Collines:

                // Si aucun picking n'a été effectué,
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
        float frequence =
            2f * Mathf.PI / dimension;


        for (int i = 0; i < p_vertices.Length; i++)
        {
            float x = p_vertices[i].x;
            float z = p_vertices[i].z;


            float hauteur =
                Mathf.Sin(x * frequence * 2f)
                *
                Mathf.Sin(z * frequence * 2f)
                *
                hauteurSinusoide;


            p_vertices[i].y = hauteur;
        }
    }


    // --------------------------------------------------------------------
    // COLLINE GAUSSIENNE
    // --------------------------------------------------------------------

    private void appliquerColline(
        float centreX,
        float centreZ)
    {
        float sigma = Mathf.Max(
            0.01f,
            largeurColline
        );


        for (int i = 0; i < p_vertices.Length; i++)
        {
            float dx =
                p_vertices[i].x - centreX;

            float dz =
                p_vertices[i].z - centreZ;


            float distanceCarree =
                dx * dx + dz * dz;


            // Fonction gaussienne :
            //
            // exp(-(x²+z²)/(2*sigma²))
            //
            float facteur =
                Mathf.Exp(
                    -distanceCarree /
                    (2f * sigma * sigma)
                );


            p_vertices[i].y =
                hauteurColline *
                facteur;
        }
    }


    // --------------------------------------------------------------------
    // PERLIN NOISE
    // --------------------------------------------------------------------

    private void appliquerPerlin()
    {
        for (int i = 0; i < p_vertices.Length; i++)
        {
            float x =
                p_vertices[i].x *
                echellePerlin +
                p_seedPerlin;

            float z =
                p_vertices[i].z *
                echellePerlin +
                p_seedPerlin;


            float bruit =
                Mathf.PerlinNoise(x, z);


            // Perlin retourne [0 ; 1].
            // On le centre autour de 0.
            float hauteur =
                (bruit - 0.5f)
                *
                2f
                *
                hauteurPerlin;


            p_vertices[i].y =
                hauteur;
        }
    }


    // --------------------------------------------------------------------
    // DEFORMATION PAR HEIGHTMAP
    // --------------------------------------------------------------------

    private void appliquerDeformation_Texture()
    {
        if (p_vertices == null)
            return;


        if (textures == null ||
            textures.Count == 0)
        {
            Debug.LogWarning(
                "Aucune HeightMap n'est renseignée."
            );

            return;
        }


        numTexture =
            Mathf.Clamp(
                numTexture,
                0,
                textures.Count - 1
            );


        Texture2D texture =
            textures[numTexture];


        if (texture == null)
        {
            Debug.LogWarning(
                "La HeightMap sélectionnée est vide."
            );

            return;
        }


        remettreTerrainPlat();


        for (int i = 0; i < p_vertices.Length; i++)
        {
            Vector2 uv = p_uv[i];


            Color couleur;

            try
            {
                couleur =
                    texture.GetPixelBilinear(
                        uv.x,
                        uv.y
                    );
            }
            catch
            {
                Debug.LogError(
                    "Impossible de lire la HeightMap. " +
                    "Vérifie que Read/Write est activé " +
                    "dans les propriétés de la texture."
                );

                return;
            }


            // Conversion en niveau de gris.
            float gris =
                couleur.grayscale;


            p_vertices[i].y =
                gris * hauteurTexture;
        }


        recalculerToutesLesNormales();

        appliquerMesh();
    }


    // --------------------------------------------------------------------
    // NORMALE D'UN TRIANGLE
    // --------------------------------------------------------------------

    private Vector3 calculerNormaleTriangle(
        int triangleIndex)
    {
        int i0 =
            p_triangles[triangleIndex];

        int i1 =
            p_triangles[triangleIndex + 1];

        int i2 =
            p_triangles[triangleIndex + 2];


        Vector3 v0 = p_vertices[i0];
        Vector3 v1 = p_vertices[i1];
        Vector3 v2 = p_vertices[i2];


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
        int triangleIndex)
    {
        int i0 =
            p_triangles[triangleIndex];

        int i1 =
            p_triangles[triangleIndex + 1];

        int i2 =
            p_triangles[triangleIndex + 2];


        Vector3 v0 = p_vertices[i0];
        Vector3 v1 = p_vertices[i1];
        Vector3 v2 = p_vertices[i2];


        Vector3 a = v1 - v0;
        Vector3 b = v2 - v0;


        return Vector3.Cross(a, b).magnitude * 0.5f;
    }


    // --------------------------------------------------------------------
    // ANGLE DU TRIANGLE AU NIVEAU DU VERTEX
    // --------------------------------------------------------------------

    private float calculerAngleAuVertex(
        int triangleIndex,
        int vertex)
    {
        int i0 =
            p_triangles[triangleIndex];

        int i1 =
            p_triangles[triangleIndex + 1];

        int i2 =
            p_triangles[triangleIndex + 2];


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
            p_vertices[autre1]
            -
            p_vertices[vertex];

        Vector3 b =
            p_vertices[autre2]
            -
            p_vertices[vertex];


        if (a.sqrMagnitude < 0.000001f ||
            b.sqrMagnitude < 0.000001f)
        {
            return 0f;
        }


        return Vector3.Angle(a, b);
    }


    // --------------------------------------------------------------------
    // CALCUL DE LA NORMALE D'UN VERTEX
    // --------------------------------------------------------------------

    private void calculerNormaleVertex(
        uint num_Vertex)
    {
        int vertex =
            (int)num_Vertex;


        if (vertex < 0 ||
            vertex >= p_vertices.Length)
            return;


        List<int> triangles =
            p_trianglesParVertex[vertex];


        if (triangles == null ||
            triangles.Count == 0)
        {
            p_normals[vertex] =
                Vector3.up;

            return;
        }


        Vector3 normaleFinale =
            Vector3.zero;


        // ------------------------------------------------------------
        // A - Moyenne simple
        // ------------------------------------------------------------

        if (modeNormale == ModeNormale.Basique)
        {
            foreach (int triangleIndex in triangles)
            {
                normaleFinale +=
                    calculerNormaleTriangle(
                        triangleIndex
                    );
            }
        }


        // ------------------------------------------------------------
        // B - Moyenne pondérée par la surface
        // ------------------------------------------------------------

        else if (modeNormale == ModeNormale.Surface)
        {
            foreach (int triangleIndex in triangles)
            {
                Vector3 normale =
                    calculerNormaleTriangle(
                        triangleIndex
                    );

                float surface =
                    calculerSurfaceTriangle(
                        triangleIndex
                    );

                normaleFinale +=
                    normale * surface;
            }
        }


        // ------------------------------------------------------------
        // C - Moyenne pondérée par l'angle
        // ------------------------------------------------------------

        else if (modeNormale == ModeNormale.Angle)
        {
            foreach (int triangleIndex in triangles)
            {
                Vector3 normale =
                    calculerNormaleTriangle(
                        triangleIndex
                    );

                float angle =
                    calculerAngleAuVertex(
                        triangleIndex,
                        vertex
                    );

                normaleFinale +=
                    normale * angle;
            }
        }


        if (normaleFinale.sqrMagnitude <
            0.000001f)
        {
            p_normals[vertex] =
                Vector3.up;
        }
        else
        {
            p_normals[vertex] =
                normaleFinale.normalized;
        }
    }


    // --------------------------------------------------------------------
    // RECALCUL DE TOUTES LES NORMALES
    // --------------------------------------------------------------------

    private void recalculerToutesLesNormales()
    {
        if (p_vertices == null ||
            p_trianglesParVertex == null)
            return;


        for (uint i = 0;
             i < p_vertices.Length;
             i++)
        {
            calculerNormaleVertex(i);
        }
    }


    // --------------------------------------------------------------------
    // PICKING TERRAIN
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


    // --------------------------------------------------------------------
    // UPDATE
    // --------------------------------------------------------------------

    void Update()
    {
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


        // ------------------------------------------------------------
        // F1 : aide
        // ------------------------------------------------------------

        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            p_afficherAide =
                !p_afficherAide;
        }


        // ------------------------------------------------------------
        // F2 : fonctions
        // ------------------------------------------------------------

        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            choixModeDeformation =
                ChoixModeDeformation.Fonction;

            appliquerDeformation_Fonction();


            typeFonction =
                (TypeFonction)
                (
                    ((int)typeFonction + 1)
                    % 3
                );
        }


        // ------------------------------------------------------------
        // F3 : HeightMaps
        // ------------------------------------------------------------

        if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            choixModeDeformation =
                ChoixModeDeformation.Texture;

            appliquerDeformation_Texture();


            if (textures != null &&
                textures.Count > 0)
            {
                numTexture =
                    (numTexture + 1)
                    % textures.Count;
            }
        }


        // ------------------------------------------------------------
        // F10 : normales
        // ------------------------------------------------------------

        if (Keyboard.current.f10Key.wasPressedThisFrame)
        {
            p_modeAffichageNormales++;

            if (p_modeAffichageNormales > 3)
                p_modeAffichageNormales = 0;


            p_tempsAffichageNormales =
                DUREE_AFFICHAGE_NORMALES;
        }


        // ------------------------------------------------------------
        // F12 : méthode de calcul des normales
        // ------------------------------------------------------------

        if (Keyboard.current.f12Key.wasPressedThisFrame)
        {
            modeNormale =
                (ModeNormale)
                (
                    ((int)modeNormale + 1)
                    % 3
                );


            recalculerToutesLesNormales();

            appliquerMesh();


            Debug.Log(
                "Mode normale : "
                + modeNormale
            );
        }
    }


    // --------------------------------------------------------------------
    // DOUBLE F11
    // --------------------------------------------------------------------

    private void gererDoubleF11()
    {
        if (Keyboard.current == null)
            return;


        if (Keyboard.current.f11Key.wasPressedThisFrame)
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


        // Une colline est ajoutée
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

        // --- DÉPLACEMENT ZQSD PHYSIQUE (Touches QWERTY WASD) ---
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

        // R tourne le terrain. (Le R est à la même place en AZERTY et QWERTY)
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


        // Pour éviter de dessiner plusieurs milliers
        // de lignes à chaque frame.
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
            // Mode 1 : normale d'éclairage
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
            // orientation + éclairage
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
        if (!p_afficherAide)
            return;


        GUI.Box(
            new Rect(
                20,
                20,
                460,
                1100
            ),
            ""
        );


        GUILayout.BeginArea(
            new Rect(
                40,
                35,
                420,
                1090
            )
        );

        GUILayout.Label(
            "INFORMATIONS DU MAILLAGE"
        );


        if (p_vertices != null)
        {
            GUILayout.Label(
                $"Vertices : {p_vertices.Length}"
                );

            GUILayout.Label(
                $"Triangles : {p_triangles.Length / 3}"
                );

            GUILayout.Label(
                $"Resolution : {resolution} x {resolution}"
                );

            GUILayout.Label(
                $"Mémoire approximative : {calculerMemoireMesh()} Ko"
                );
        }

        GUILayout.Space(10);

        GUILayout.Label(
            "PARAMETRES"
            );

        GUILayout.Label(
            $"Mode : {choixModeDeformation}"
            );

        GUILayout.Label(
            $"Fonction : {typeFonction}"
            );

        GUILayout.Label(
            $"Normales : {modeNormale}"
            );

        GUILayout.Space(10);

        GUILayout.Label(
            "INTERACTIONS"
            );

        GUILayout.Label(
            "F1 : Aide / informations"
            );

        GUILayout.Label(
            "F2 : Fonction suivante"
            );

        GUILayout.Label(
            "F3 : HeightMap suivante"
            );

        GUILayout.Label(
            "F10 : Afficher les normales"
            );

        GUILayout.Label(
            "F11 x2 : Revenir au terrain plat"
            );

        GUILayout.Label(
            "F12 : Changer le calcul des normales"
            );

        GUILayout.Space(5);

        GUILayout.Label(
            "Clic gauche : Placer une colline"
            );

        GUILayout.Label(
            "ZQSD : Déplacer la caméra"
            );

        GUILayout.Label(
            "E / A : Monter / Descendre"
            );

        GUILayout.Label(
            "O/K/L/M : Tourner la caméra"
            );

        GUILayout.Label(
            "R : Faire tourner le terrain"
            );

        GUILayout.Space(10);

        GUILayout.Label(
            "SCULPTURE INTERACTIVE (EXERCICE 1)"
            );

        GUILayout.Label(
            $"Pattern Actif : {p_indexPatternCourant}"
            );

        GUILayout.Label(
            $"Intensité Max : {intensiteMaxDeformation}"
            );

        GUILayout.Label(
            $"Rayon : {rayonDeformation}"
            );

        GUILayout.Space(10);

        GUILayout.Label(
            "INTERACTIONS SCULPTURE"
            );

        GUILayout.Label(
            "Clic Gauche : Sculpter (Elévation)"
            );

        GUILayout.Label(
            "Clic Droit : Sculpter (Dépression)"
            );

        GUILayout.Label(
            "Touche Espace : Prévisualiser la zone du pattern (Debug)"
            );

        GUILayout.Label(
            "Molette + SHIFT : Varier l'intensité"
            );

        GUILayout.Label(
            "Molette + CTRL : Varier le rayon"
            );

        GUILayout.Label(
            "Molette + ALT : Changer de pattern"
            );


        GUILayout.Space(10);
        GUILayout.Label("PARAMETRES LOD :");

        // Affichage si le LOD est actif ou pas
        LODGroup lodGroup = GetComponent<LODGroup>();
        if ( lodGroup != null )
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
    // PATTERN DE DEFORMATION
    // --------------------------------------------------------------------

    private void appliquerPatternDeformation(
    Vector3 pointMonde,
    bool elevation)
{
    if (p_vertices == null)
        return;


    if (patternsDeformation == null ||
        patternsDeformation.Length == 0)
        return;


    AnimationCurve courbe =
        patternsDeformation[p_indexPatternCourant];


    if (courbe == null)
    {
        Debug.LogWarning(
            "Le pattern "
            + p_indexPatternCourant
            + " est vide."
        );

        return;
    }


    // ------------------------------------------------------------
    // Point de collision en espace local
    // ------------------------------------------------------------

    Vector3 centreLocal =
        transform.InverseTransformPoint(
            pointMonde
        );


    float rayon =
        Mathf.Max(
            0.01f,
            rayonDeformation
        );


    float rayonCarre =
        rayon * rayon;


    // ------------------------------------------------------------
    // Direction de la déformation
    // ------------------------------------------------------------

    float direction =
        elevation
        ? 1f
        : -1f;


    float forceMax =
        intensiteMaxDeformation
        * Time.deltaTime
        * 5f;


    bool maillageModifie = false;


    // ------------------------------------------------------------
    // Recherche des vertices dans le voisinage
    // ------------------------------------------------------------

    for (int i = 0;
         i < p_vertices.Length;
         i++)
    {
        float dx =
            p_vertices[i].x
            - centreLocal.x;


        float dz =
            p_vertices[i].z
            - centreLocal.z;


        float distanceCarree =
            dx * dx
            + dz * dz;


        // Vertex en dehors du cercle
        if (distanceCarree > rayonCarre)
            continue;


        // --------------------------------------------------------
        // Distance normalisée [0 ; 1]
        // --------------------------------------------------------

        float distance =
            Mathf.Sqrt(distanceCarree);


        float t =
            distance / rayon;


        // --------------------------------------------------------
        // Application du pattern
        // --------------------------------------------------------

        float force =
            courbe.Evaluate(t);


        float deformation =
            direction
            * force
            * forceMax;


        p_vertices[i].y +=
            deformation;


        maillageModifie = true;
    }


    // ------------------------------------------------------------
    // Mise à jour du terrain
    // ------------------------------------------------------------

    if (maillageModifie)
    {
        recalculerToutesLesNormales();

        appliquerMesh();
    }
}

    // --------------------------------------------------------------------
    // PREVISUALISATION DE LA DEFORMATION
    // --------------------------------------------------------------------

    private void previsualiserDeformation(Vector3 pointMonde)
    {
        Vector3 centreLocal = transform.InverseTransformPoint(pointMonde);
        float sqrRayon = rayonDeformation * rayonDeformation;

        for (int i = 0; i < p_vertices.Length; i++)
        {
            float dx = p_vertices[i].x - centreLocal.x;
            float dz = p_vertices[i].z - centreLocal.z;

            if ((dx * dx + dz * dz) <= sqrRayon)
            {
                Vector3 positionMondeVertex = transform.TransformPoint(p_vertices[i]);
                Debug.DrawLine(positionMondeVertex, positionMondeVertex + Vector3.up * 1f, Color.magenta);
            }
        }
    }
}


