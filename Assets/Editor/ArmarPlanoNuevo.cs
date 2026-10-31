using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// arma Apartamento2.unity desde Apartamento.unity cambiando solo la casa: en lugar del modelo viejo van las 4 casas
// del plano de Blender (Casas.fbx, exportado de Revit), su lote con el asfalto y los carros, y un valle plano con
// montanas de fondo. la casa 1 (la de los carros) funciona como la vieja con los mismos scripts: WallSides de
// Felipe pinta sus paredes por dentro y por fuera, sus brochas, puertas con HingedDoor y muebles que se mueven.
// las otras 3 casas solo se ven. Apartamento.unity no se toca. se puede correr otra vez y rehace la copia
public static class ArmarPlanoNuevo
{
    const string escenaOriginal = "Assets/Scenes/Apartamento.unity";
    const string escenaNueva = "Assets/Scenes/Apartamento2.unity";
    const string modelo = "Assets/Modelos/Plano nuevo/Casas.fbx";
    const string carpeta = "Assets/Modelos/Plano nuevo";
    const string terrenoMuestra = "Assets/Fantasy Skybox FREE/Scenes/Textures (Terrain)/SampleTerrain.asset";

    // centro de la planta baja de la casa vieja: ahi queda la casa 1, con el piso a la misma altura
    static readonly Vector3 centroCasa = new Vector3(3.5f, 0f, 4.5f);

    // el plano se agranda un poco para que adentro se pueda caminar entre los muebles (los muebles no se agrandan)
    const float escala = 1.25f;

    // en Revit las casas van una tras otra cada 7.98 m
    const float separacionCasas = 7.98f * escala;

    // terreno: valle plano alrededor del lote (donde se teletransporta) y montanas de fondo
    const float ladoTerreno = 600f;
    const float radioValle = 60f;
    const float alturaMontanas = 45f;
    const int cantidadArboles = 40;

    // a que distancia de la cabeza sale el menu de bienvenida (en el apartamento es 2.4 y quedaba lejos)
    const float distanciaMenu = 1.5f;

    // capa de interaccion "Teleport" (bit 31), la misma de los pisos de la escena vieja
    const int capaTeleport = 1 << 31;

    // huecos de las puertas de la casa 1, para ponerles piso abajo
    static readonly List<Bounds> huecosDePuertas = new List<Bounds>();

    [MenuItem("Herramientas/Armar escena del plano nuevo")]
    static void Armar()
    {
        if (!Application.isBatchMode && AssetDatabase.LoadAssetAtPath<SceneAsset>(escenaNueva) != null &&
            !EditorUtility.DisplayDialog("Plano nuevo", escenaNueva + " ya existe. ¿Rehacerla desde Apartamento.unity?", "Rehacer", "Cancelar"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelo);
        if (prefab == null)
        {
            Debug.LogError("No se encontro " + modelo);
            return;
        }

        AssetDatabase.DeleteAsset(escenaNueva);
        AssetDatabase.CopyAsset(escenaOriginal, escenaNueva);
        Scene escena = EditorSceneManager.OpenScene(escenaNueva, OpenSceneMode.Single);

        // de la casa vieja solo se guardan una brocha de piso y una de pared, para copiarlas en la casa nueva
        MaterialChanger brochaPiso = Guardar(escena, "Pisos por sala");
        MaterialChanger brochaPared = Guardar(escena, "Brochas de pared");
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            if (raiz.name == "Apartamento" || raiz.name == "Pisos por sala" || raiz.name == "Brochas de pared" || raiz.name == "Patio")
                Object.DestroyImmediate(raiz);
        }

        GameObject casas = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
        // se desempaca para poder partir paredes y puertas
        PrefabUtility.UnpackPrefabInstance(casas, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        casas.name = "Plano nuevo";
        casas.transform.localScale *= escala;
        Alinear(casas.transform);
        Transform casa1 = Agrupar(casas.transform);

        huecosDePuertas.Clear();

        // materiales, colliders y teletransporte de todo el plano
        List<Renderer> puertas = new List<Renderer>();
        List<Renderer> muebles = new List<Renderer>();
        List<Renderer> paredes = new List<Renderer>();
        foreach (Renderer pieza in casas.GetComponentsInChildren<Renderer>())
        {
            pieza.sharedMaterial = MaterialPara(pieza.name);
            bool enCasa1 = pieza.transform.IsChildOf(casa1);
            if (EsPuerta(pieza.name)) puertas.Add(pieza);
            else if (enCasa1 && EsMueble(pieza.name)) muebles.Add(pieza);
            else if (pieza.name.StartsWith("18039_Coniferous"))
            {
                // sin collider: con la malla de las ramas uno se quedaba pegado
                GameObjectUtility.SetStaticEditorFlags(pieza.gameObject, (StaticEditorFlags)~0);
            }
            else if (enCasa1 && pieza.name.StartsWith("Muro"))
            {
                // no Batching Static: WallSides no puede partir mallas que Unity junto en el build
                paredes.Add(pieza);
                pieza.gameObject.AddComponent<MeshCollider>();
                GameObjectUtility.SetStaticEditorFlags(pieza.gameObject, (StaticEditorFlags)~0 & ~StaticEditorFlags.BatchingStatic);
            }
            else
            {
                if (enCasa1 && pieza.name.StartsWith("Suelo piso porcelanato"))
                {
                    // la brocha de piso pinta solo la cara de arriba; la orilla de la losa (que se ve en la fachada)
                    // queda del color de la pared
                    // la orilla de afuera (slot 1) la pinta la brocha de la fachada; el cielo de abajo y las orillas de
                    // adentro, como la del hueco de las gradas (slot 2), quedan igual
                    Bounds borde = pieza.bounds;
                    borde.Expand(new Vector3(-0.1f, 0f, -0.1f));
                    PorMateriales(pieza, (i, centro, n) => n.y > 0.7f ? 0 : Mathf.Abs(n.y) < 0.3f && !borde.Contains(new Vector3(centro.x, borde.center.y, centro.z)) ? 1 : 2, new Material[] { DeLaEscena("Piso gris claro"), DeLaEscena("Repello marfil"), DeLaEscena("Repello marfil") }, "Pisos");

                    // 1 cm mas chica por lado: su orilla quedaba en el mismo plano que la pared y parpadeaba al pintarla
                    Vector3 tamano = pieza.bounds.size;
                    pieza.transform.localScale = Vector3.Scale(pieza.transform.localScale, new Vector3((tamano.x - 0.02f) / tamano.x, 1f, (tamano.z - 0.02f) / tamano.z));
                }
                if (pieza.name.StartsWith("Blocks_Jardinera"))
                {
                    // cada planta es una pieza suelta adentro de la maceta: lo que no es planta queda de concreto
                    HashSet<int> plantas = Plantas(pieza);
                    DosMateriales(pieza, (i, centro, n) => !plantas.Contains(i), pieza.sharedMaterial, Propio("Materiales", "Hojas", new Color(0.2f, 0.45f, 0.16f), 0.2f, 0f), "Jardineras");
                }
                Fijo(pieza.gameObject);
            }
        }

        CrearTerreno(escena, casas.transform);
        Physics.SyncTransforms();
        foreach (Renderer pieza in casa1.GetComponentsInChildren<Renderer>())
        {
            if (EsMueble(pieza.name) || pieza.name.StartsWith("Cocina") || pieza.name.StartsWith("Refrigerator") || pieza.name.StartsWith("Lavadora"))
                TamanoReal(pieza);
            if (pieza.name.StartsWith("Cocina completa"))
                SinCampana(pieza);
        }
        foreach (Renderer pieza in casas.GetComponentsInChildren<Renderer>())
        {
            if (pieza.name.StartsWith("Escalera moldeada") && !pieza.name.Contains("_["))
                NoTeletransportarDebajo(pieza);
        }
        Faroles(casas.transform);
        Physics.SyncTransforms();

        foreach (Renderer puerta in puertas)
            ArmarPuerta(puerta, puerta.transform.IsChildOf(casa1));

        foreach (Renderer mueble in muebles)
        {
            PartesDelMueble(mueble);
            ArmarMueble(mueble, brochaPared.radialMenuPrefab, AcabadosPara(mueble.name));
        }

        // WallSides de Felipe parte las paredes de la casa 1 en dentro y fuera al empezar, igual que en el apartamento
        List<Renderer> tramos = paredes;
        foreach (Bounds hueco in huecosDePuertas)
            Umbral(hueco, casa1);
        WallSides lados = Object.FindFirstObjectByType<WallSides>();
        if (lados != null)
            lados.house = casa1;

        Physics.SyncTransforms();
        int brochas = ArmarBrochas(casa1, tramos, brochaPiso, brochaPared);
        MoverLuces(escena, casa1);

        Vector3 inicio = Inicio(casa1);
        foreach (WelcomeMenu menu in Object.FindObjectsByType<WelcomeMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            menu.tourStart = inicio;
            menu.spawnDistance = distanciaMenu;
            if (menu.GetComponent<TituloBienvenida>() == null)
                menu.gameObject.AddComponent<TituloBienvenida>();
        }
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            if (raiz.name == "VR Player")
                raiz.transform.SetPositionAndRotation(inicio, Quaternion.identity);
        }

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("Plano nuevo listo: " + puertas.Count + " puertas, " + muebles.Count + " muebles, " + tramos.Count + " tramos de pared, " + brochas + " brochas. inicio en " + inicio);
    }

    // saca una brocha de la casa vieja a la raiz de la escena, para usarla de modelo
    static MaterialChanger Guardar(Scene escena, string grupo)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            if (raiz.name != grupo) continue;

            MaterialChanger brocha = raiz.GetComponentInChildren<MaterialChanger>(true);
            brocha.transform.SetParent(null, true);
            return brocha;
        }
        return null;
    }

    // ---------- el plano ----------

    // endereza las casas (en Revit vienen giradas) y pone la planta baja de la casa 1 donde estaba la casa vieja.
    // la casa 1 es la que tiene la cocina; la fila de casas queda hacia +x
    static void Alinear(Transform casas)
    {
        Vector3 cocina = Vector3.zero;
        List<Bounds> losas = new List<Bounds>();
        foreach (Renderer pieza in casas.GetComponentsInChildren<Renderer>())
        {
            if (pieza.name.StartsWith("Cocina completa"))
                cocina = pieza.bounds.center;
            if (pieza.name.StartsWith("Suelo piso porcelanato") && pieza.bounds.max.y < 1f)
                losas.Add(pieza.bounds);
        }

        Bounds primera = losas[0];
        foreach (Bounds losa in losas)
        {
            if (losa.min.x < cocina.x && cocina.x < losa.max.x && losa.min.z < cocina.z && cocina.z < losa.max.z)
                primera = losa;
        }
        Bounds vecina = primera;
        float cerca = float.MaxValue;
        foreach (Bounds losa in losas)
        {
            float d = Vector3.Distance(losa.center, primera.center);
            if (d > 1f && d < cerca)
            {
                cerca = d;
                vecina = losa;
            }
        }

        Vector3 fila = vecina.center - primera.center;
        // de a 90 grados: Blender ya dejo las paredes a lo largo de los ejes (la fila de casas va un poco sesgada)
        float giro = Mathf.Round(Vector3.SignedAngle(new Vector3(fila.x, 0f, fila.z), Vector3.right, Vector3.up) / 90f) * 90f;
        casas.RotateAround(primera.center, Vector3.up, giro);
        casas.position += new Vector3(centroCasa.x - primera.center.x, centroCasa.y - primera.max.y, centroCasa.z - primera.center.z);
    }

    // ordena el plano en "Casa 1" a "Casa 4" (de izquierda a derecha) y "Lote" (suelos, asfalto, postes, carros,
    // jardineras y el pino) para que se encuentre facil en la jerarquia. devuelve la casa 1
    static Transform Agrupar(Transform casas)
    {
        string[] nombres = { "Lote", "Casa 1", "Casa 2", "Casa 3", "Casa 4" };
        Transform[] grupos = new Transform[nombres.Length];
        for (int i = 0; i < nombres.Length; i++)
        {
            grupos[i] = new GameObject(nombres[i]).transform;
            grupos[i].SetParent(casas, false);
        }

        List<Transform> piezas = new List<Transform>();
        foreach (Transform hijo in casas)
        {
            if (System.Array.IndexOf(grupos, hijo) < 0)
                piezas.Add(hijo);
        }

        foreach (Transform pieza in piezas)
        {
            Renderer[] partes = pieza.GetComponentsInChildren<Renderer>();
            if (partes.Length == 0) continue;

            Bounds caja = Caja(partes);
            int casa = Mathf.RoundToInt((caja.center.x - centroCasa.x) / separacionCasas) + 1;
            bool lote = Mathf.Max(caja.size.x, caja.size.z) > 12f * escala || casa < 1 || casa > 4 || EsDelLote(pieza.name);
            pieza.SetParent(grupos[lote ? 0 : casa], true);
        }
        return grupos[1];
    }

    static bool EsDelLote(string nombre)
    {
        return nombre.StartsWith("Audi_") || nombre.StartsWith("Poste") || nombre.StartsWith("Blocks_Jardinera") || nombre.StartsWith("18039_Coniferous");
    }

    // colliders para todo lo que no se mueve, y teletransporte en lo que se camina (pisos, gradas, suelos del lote)
    static void Fijo(GameObject pieza)
    {
        GameObjectUtility.SetStaticEditorFlags(pieza, (StaticEditorFlags)~0);
        // los carros con su caja: la malla tiene huecos y uno se quedaba trabado encima
        if (pieza.name.StartsWith("Audi_"))
        {
            pieza.AddComponent<BoxCollider>();
            return;
        }
        MeshCollider collider = pieza.AddComponent<MeshCollider>();

        string nombre = pieza.name;
        if (nombre.StartsWith("Suelo") || nombre.StartsWith("Escalera") || nombre.StartsWith("Tramo"))
        {
            WorldUpTeleportationArea area = pieza.AddComponent<WorldUpTeleportationArea>();
            area.colliders.Add(collider);
            area.interactionLayers = capaTeleport;
        }
    }

    static Bounds Caja(Renderer[] partes)
    {
        Bounds caja = partes[0].bounds;
        foreach (Renderer parte in partes)
            caja.Encapsulate(parte.bounds);
        return caja;
    }

    static bool EsPuerta(string nombre)
    {
        return nombre.StartsWith("Puerta de 1 hoja") || nombre.StartsWith("Aluprof_MB-86N");
    }

    // los muebles de la casa 1 que se agarran y cambian de acabado, como la mesita de la casa vieja
    static bool EsMueble(string nombre)
    {
        string[] muebles = { "Sof", "Taburete", "modern_center_table", "Table-Coffee", "green office chair", "Estaci", "CAMA", "Bed_", "Guarda" };
        foreach (string mueble in muebles)
        {
            if (nombre.StartsWith(mueble)) return true;
        }
        return false;
    }

    // ---------- paredes ----------

    // el piso debajo de una puerta (ahi la losa no llega, llega hasta la pared): una lamina al ras del piso, con su
    // material y la misma textura en metros que la losa, que la brocha del piso pinta junto con la losa
    static void Umbral(Bounds hueco, Transform padre)
    {
        Vector3 a = new Vector3(hueco.min.x, hueco.min.y + 0.003f, hueco.min.z);
        Vector3 b = new Vector3(hueco.max.x, a.y, hueco.max.z);
        Vector3 centro = (a + b) / 2f;
        Mesh malla = new Mesh();
        malla.vertices = new Vector3[] { new Vector3(a.x, a.y, a.z) - centro, new Vector3(a.x, a.y, b.z) - centro, new Vector3(b.x, a.y, b.z) - centro, new Vector3(b.x, a.y, a.z) - centro };
        malla.normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        malla.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        malla.uv = UVsDeMundo(malla, centro);
        malla.RecalculateBounds();
        Guardar(malla, "Pisos", "Umbral " + hueco.center.ToString("F2"));

        GameObject umbral = new GameObject("Suelo umbral", typeof(MeshFilter), typeof(MeshRenderer));
        umbral.transform.SetParent(padre, false);
        umbral.transform.SetPositionAndRotation(centro, Quaternion.identity);
        umbral.transform.localScale = Vector3.one;
        umbral.transform.localScale = new Vector3(1f / umbral.transform.lossyScale.x, 1f / umbral.transform.lossyScale.y, 1f / umbral.transform.lossyScale.z);
        umbral.GetComponent<MeshFilter>().sharedMesh = malla;
        umbral.GetComponent<Renderer>().sharedMaterial = DeLaEscena("Piso gris claro");
        Fijo(umbral);
    }

    // parte la malla de la pieza en dos materiales: los triangulos que cumplen "primero" van al slot 0 con el
    // primer material, el resto al slot 1. la pieza queda en coordenadas de mundo, sin girar
    static void DosMateriales(Renderer pieza, System.Func<int, Vector3, Vector3, bool> primero, Material material0, Material material1, string subcarpeta)
    {
        PorMateriales(pieza, (i, centro, n) => primero(i, centro, n) ? 0 : 1, new Material[] { material0, material1 }, subcarpeta);
    }

    // parte la malla de la pieza en varios materiales: cada triangulo va al slot que diga "slot". la pieza queda en
    // coordenadas de mundo, sin girar
    static void PorMateriales(Renderer pieza, System.Func<int, Vector3, Vector3, int> slot, Material[] materiales, string subcarpeta)
    {
        Mesh original = pieza.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = original.vertices;
        Vector3[] normales = original.normals;
        int[] triangulos = original.triangles;
        Transform t = pieza.transform;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = t.TransformPoint(vertices[i]);
            normales[i] = t.TransformDirection(normales[i]).normalized;
        }

        List<int>[] porSlot = new List<int>[materiales.Length];
        for (int k = 0; k < porSlot.Length; k++)
            porSlot[k] = new List<int>();
        for (int i = 0; i < triangulos.Length; i += 3)
        {
            Vector3 a = vertices[triangulos[i]];
            Vector3 b = vertices[triangulos[i + 1]];
            Vector3 c = vertices[triangulos[i + 2]];
            porSlot[slot(i / 3, (a + b + c) / 3f, Vector3.Cross(b - a, c - a).normalized)].AddRange(new int[] { triangulos[i], triangulos[i + 1], triangulos[i + 2] });
        }
        if (porSlot[0].Count == 0 || porSlot[1].Count == 0) return;

        Vector3 centro = pieza.bounds.center;
        CombineInstance[] partes = new CombineInstance[materiales.Length];
        for (int k = 0; k < partes.Length; k++)
            partes[k] = new CombineInstance { mesh = Malla(vertices, normales, porSlot[k], centro) };
        Mesh junta = new Mesh();
        junta.CombineMeshes(partes, false, false);
        junta.uv = UVsDeMundo(junta, centro);
        Guardar(junta, subcarpeta, Id(pieza.name));

        pieza.GetComponent<MeshFilter>().sharedMesh = junta;
        pieza.transform.SetPositionAndRotation(centro, Quaternion.identity);
        pieza.transform.localScale = Vector3.one;
        pieza.transform.localScale = new Vector3(1f / pieza.transform.lossyScale.x, 1f / pieza.transform.lossyScale.y, 1f / pieza.transform.lossyScale.z);
        pieza.sharedMaterials = materiales;
    }

    // UVs en metros segun hacia donde mira cada cara (como WallSides), para que las texturas no se estiren
    static Vector2[] UVsDeMundo(Mesh malla, Vector3 centro)
    {
        Vector3[] vertices = malla.vertices;
        Vector3[] normales = malla.normals;
        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 punto = vertices[i] + centro;
            Vector3 n = normales[i];
            if (Mathf.Abs(n.y) >= Mathf.Abs(n.x) && Mathf.Abs(n.y) >= Mathf.Abs(n.z))
                uvs[i] = new Vector2(punto.x, punto.z) / 2f;
            else if (Mathf.Abs(n.x) >= Mathf.Abs(n.z))
                uvs[i] = new Vector2(punto.z, punto.y) / 2f;
            else
                uvs[i] = new Vector2(punto.x, punto.y) / 2f;
        }
        return uvs;
    }

    // malla nueva con esos triangulos y solo sus vertices, en coordenadas de mundo menos el centro
    static Mesh Malla(Vector3[] vertices, Vector3[] normales, List<int> triangulos, Vector3 centro)
    {
        Dictionary<int, int> nuevo = new Dictionary<int, int>();
        List<Vector3> v = new List<Vector3>();
        List<Vector3> n = new List<Vector3>();
        int[] indices = new int[triangulos.Count];
        for (int i = 0; i < triangulos.Count; i++)
        {
            int indice;
            if (!nuevo.TryGetValue(triangulos[i], out indice))
            {
                indice = v.Count;
                nuevo[triangulos[i]] = indice;
                v.Add(vertices[triangulos[i]] - centro);
                n.Add(normales[triangulos[i]]);
            }
            indices[i] = indice;
        }

        Mesh malla = new Mesh();
        malla.SetVertices(v);
        malla.SetNormals(n);
        malla.SetUVs(0, new Vector2[v.Count]);
        malla.triangles = indices;
        malla.RecalculateBounds();
        return malla;
    }

    // guarda la malla en Plano nuevo/<subcarpeta>, para que la escena no cargue con ella
    static void Guardar(Mesh malla, string subcarpeta, string nombre)
    {
        if (!AssetDatabase.IsValidFolder(carpeta + "/" + subcarpeta))
            AssetDatabase.CreateFolder(carpeta, subcarpeta);
        AssetDatabase.CreateAsset(malla, carpeta + "/" + subcarpeta + "/" + nombre + ".asset");
    }

    // el numero de Revit que va entre corchetes al final del nombre
    static string Id(string nombre)
    {
        return nombre.Substring(nombre.LastIndexOf('[') + 1).TrimEnd(']');
    }

    // ---------- puertas ----------

    // Revit trae en cada puerta un panel de una sola cara perpendicular a la pared (el dibujo de la puerta
    // abierta): se quita. en la casa 1 la hoja se cambia por una como la de HouseInteractions (hoja lisa con
    // manillas en la punta contraria a la bisagra) que abre con HingedDoor hacia adentro del cuarto. en las otras
    // casas la puerta queda fija y cerrada
    // las puertas del cuarto sin escritorio (planta baja y alta), por su numero de Revit
    static readonly string[] bisagraAlOtroLado = { "174235", "249467" };

    static void ArmarPuerta(Renderer puerta, bool abre)
    {
        Transform pieza = puerta.transform;
        Mesh original = puerta.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = original.vertices;
        Vector3[] normales = original.normals;
        int[] triangulos = original.triangles;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = pieza.TransformPoint(vertices[i]);
            normales[i] = pieza.TransformDirection(normales[i]).normalized;
        }

        // la hoja es la placa con caras para los dos lados; su normal es la normal de la pared
        float[] areas = new float[4];
        float[] planos = new float[4];
        Vector3[] ejes = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        for (int i = 0; i < triangulos.Length; i += 3)
        {
            Vector3 a = vertices[triangulos[i]];
            Vector3 cruz = Vector3.Cross(vertices[triangulos[i + 1]] - a, vertices[triangulos[i + 2]] - a);
            for (int e = 0; e < 4; e++)
            {
                if (Vector3.Dot(cruz.normalized, ejes[e]) < 0.95f) continue;
                areas[e] += cruz.magnitude / 2f;
                planos[e] += Vector3.Dot(a, Abs(ejes[e])) * cruz.magnitude / 2f;
            }
        }
        bool enX = Mathf.Min(areas[2], areas[3]) >= Mathf.Min(areas[0], areas[1]);
        Vector3 normal = enX ? Vector3.forward : Vector3.right;
        Vector3 largo = enX ? Vector3.right : Vector3.forward;
        int cara = enX ? 2 : 0;
        float plano = (planos[cara] + planos[cara + 1]) / Mathf.Max(areas[cara] + areas[cara + 1], 0.0001f);

        // lo pegado a la pared es la puerta; lo que sale es el dibujo de la puerta abierta
        List<int> pegados = new List<int>();
        Vector3 dibujo = Vector3.zero;
        int puntosDibujo = 0;
        for (int i = 0; i < triangulos.Length; i += 3)
        {
            bool pegado = true;
            for (int j = 0; j < 3; j++)
            {
                if (Mathf.Abs(Vector3.Dot(vertices[triangulos[i + j]], normal) - plano) > 0.15f)
                    pegado = false;
            }
            if (pegado)
                pegados.AddRange(new int[] { triangulos[i], triangulos[i + 1], triangulos[i + 2] });
            else
            {
                for (int j = 0; j < 3; j++)
                    dibujo += vertices[triangulos[i + j]];
                puntosDibujo += 3;
            }
        }

        Bounds caja = new Bounds(vertices[pegados[0]], Vector3.zero);
        foreach (int indice in pegados)
            caja.Encapsulate(vertices[indice]);

        // Revit trae puertas de adentro sin pared a los lados (quedaban paradas en medio del cuarto): se quitan
        if (puerta.name.StartsWith("Puerta de 1 hoja") && !HayPared(caja, largo))
        {
            Object.DestroyImmediate(puerta.gameObject);
            return;
        }

        if (!abre || puntosDibujo == 0)
        {
            Reemplazar(puerta, Malla(vertices, normales, pegados, caja.center), caja.center, "Fija");
            Fijo(puerta.gameObject);
            return;
        }

        // la hoja mide lo que dice el nombre de la puerta ("90 x 210 cm", "1x2.10m"); lo de los lados es el marco.
        // su centro es el de sus dos caras grandes (en una esquina el marco queda cortado de un lado y no sirve)
        float anchoCaras;
        Vector3 centroHoja = caja.center + largo * (CentroDeCaras(vertices, pegados, normal, plano, largo, Vector3.Dot(caja.center, largo), out anchoCaras) - Vector3.Dot(caja.center, largo));
        // la hoja llena el marco: si sus caras miden mas que el nombre (pasa en los banos), se usa lo que miden
        float mitadHoja = Mathf.Max(AnchoHoja(puerta.name, Vector3.Dot(caja.size, largo) / escala) * escala, anchoCaras) / 2f;

        // mejor aun, el hueco del marco: entre las caras de adentro de sus dos jambas. asi la hoja llena el marco y
        // no queda rendija por donde se vea al otro lado
        float jambaIzquierda = float.MinValue;
        float jambaDerecha = float.MaxValue;
        for (int i = 0; i < pegados.Count; i += 3)
        {
            Vector3 a = vertices[pegados[i]];
            Vector3 n = Vector3.Cross(vertices[pegados[i + 1]] - a, vertices[pegados[i + 2]] - a).normalized;
            float d = Vector3.Dot(n, largo);
            float donde = Vector3.Dot(a - centroHoja, largo);
            if (Mathf.Abs(d) < 0.9f || Mathf.Abs(donde) < mitadHoja * 0.6f || Mathf.Abs(donde) > mitadHoja + 0.15f) continue;
            if (donde > 0f && d < 0f) jambaDerecha = Mathf.Min(jambaDerecha, donde);
            if (donde < 0f && d > 0f) jambaIzquierda = Mathf.Max(jambaIzquierda, donde);
        }
        Vector3 centroHueco = centroHoja;
        float mitadHueco = mitadHoja;
        if (jambaIzquierda > float.MinValue && jambaDerecha < float.MaxValue && Mathf.Abs((jambaDerecha - jambaIzquierda) / 2f - mitadHoja) < 0.15f * mitadHoja)
        {
            centroHueco += largo * (jambaIzquierda + jambaDerecha) / 2f;
            mitadHueco = (jambaDerecha - jambaIzquierda) / 2f;
        }
        List<int> hoja = new List<int>();
        List<int> marco = new List<int>();
        for (int i = 0; i < pegados.Count; i += 3)
        {
            bool adentro = true;
            for (int j = 0; j < 3; j++)
            {
                if (Mathf.Abs(Vector3.Dot(vertices[pegados[i + j]] - centroHoja, largo)) > mitadHoja + 0.005f)
                    adentro = false;
            }
            (adentro ? hoja : marco).AddRange(new int[] { pegados[i], pegados[i + 1], pegados[i + 2] });
        }

        // el marco se queda donde estaba. con su collider puesto se mide el hueco de verdad: desde el medio de la
        // puerta, rayos a cada lado y hacia arriba hasta las jambas y el cabezal. la hoja nueva llena ese hueco
        if (marco.Count > 0)
        {
            Reemplazar(puerta, Malla(vertices, normales, marco, caja.center), caja.center, "Marco");
            Fijo(puerta.gameObject);
            Physics.SyncTransforms();
        }
        Vector3 medio = largo * Vector3.Dot(centroHueco, largo) + normal * plano + Vector3.up * (caja.min.y + 1f);
        RaycastHit jamba, otraJamba, cabezal;
        if (Physics.Raycast(medio, largo, out jamba, mitadHueco + 0.25f) && Physics.Raycast(medio, -largo, out otraJamba, mitadHueco + 0.25f))
        {
            centroHueco += largo * (jamba.distance - otraJamba.distance) / 2f;
            mitadHueco = (jamba.distance + otraJamba.distance) / 2f;
        }
        float altoHueco = 2.1f * escala;
        if (Physics.Raycast(medio, Vector3.up, out cabezal, 2.5f))
            altoHueco = cabezal.point.y - caja.min.y;
        Bounds hueco = new Bounds(new Vector3(centroHueco.x, caja.min.y + (2.1f * escala + 0.05f) / 2f, centroHueco.z), largo * (mitadHueco * 2f + 0.1f) + Vector3.up * (2.1f * escala + 0.05f) + normal * 0.6f);
        huecosDePuertas.Add(hueco);

        // abre hacia adentro del cuarto (el lado con menos piso; si dan igual, para donde la dibujo Revit). la
        // bisagra va en la punta donde la hoja abre mas sin chocar, y si dan igual, en la que tiene la pared mas cerca
        dibujo /= puntosDibujo;
        int espacioAdelante = Espacio(caja, normal, 1f);
        int espacioAtras = Espacio(caja, normal, -1f);
        Vector3 abreHacia = normal * (espacioAdelante < espacioAtras ? 1f : espacioAdelante > espacioAtras ? -1f : Mathf.Sign(Vector3.Dot(dibujo, normal) - plano));

        // HingedDoor abre hacia el angulo negativo de su eje "arriba". si hay que abrir para el otro sentido, la
        // bisagra se pone de cabeza (su arriba apunta para abajo)
        Vector3 bisagra = Vector3.zero;
        Vector3 cerrada = Vector3.zero;
        float sentido = 1f;
        float angulo = -1f;
        float mejor = float.MinValue;
        foreach (float punta in new float[] { 1f, -1f })
        {
            Vector3 esta = largo * (Vector3.Dot(centroHueco, largo) + punta * (mitadHueco - 0.005f)) + normal * plano + abreHacia * 0.025f + Vector3.up * caja.min.y;
            Vector3 haciaHoja = -largo * punta;
            float signo = Vector3.Dot(Quaternion.AngleAxis(90f, Vector3.up) * haciaHoja, abreHacia) > 0f ? 1f : -1f;
            // si choca de una es la jamba pegada a la bisagra, no algo en el camino: abre completa
            float giro = AnguloLibre(esta, haciaHoja, signo, mitadHueco * 2f, caja.size.y, puerta);
            giro = giro < 10f ? 90f : Mathf.Max(giro, 45f);
            // entre dos que abren igual, la bisagra va donde la puerta abierta queda recostada en la pared y no
            // contra el lavatorio o el ropero
            // en el bano, la bisagra en la punta mas lejos del lavatorio: la puerta abierta queda contra la pared
            float lavatorio = Lavatorio(esta + abreHacia * 0.5f, pieza.root);
            float puntos = giro * 10f + Recostada(esta + abreHacia * 0.4f, largo * punta, pieza.root) + (lavatorio < 2.5f ? lavatorio * 30f : 0f);
            // como pidio Josh, la del cuarto sin escritorio (abajo y arriba) va con la bisagra en la otra punta
            if (System.Array.IndexOf(bisagraAlOtroLado, Id(puerta.name)) >= 0)
                puntos = -puntos;
            if (puntos <= mejor) continue;
            mejor = puntos;
            angulo = giro;
            bisagra = esta;
            cerrada = haciaHoja;
            sentido = signo;
        }

        GameObject puertaQueAbre = new GameObject("Puerta " + Id(puerta.name));
        puertaQueAbre.transform.SetParent(pieza.parent, false);
        puertaQueAbre.transform.SetPositionAndRotation(bisagra, sentido < 0f ? Quaternion.identity : Quaternion.Euler(180f, 0f, 0f));
        puertaQueAbre.transform.localScale = Vector3.one;
        puertaQueAbre.transform.localScale = new Vector3(1f / puertaQueAbre.transform.lossyScale.x, 1f / puertaQueAbre.transform.lossyScale.y, 1f / puertaQueAbre.transform.lossyScale.z);

        // como la puerta de HouseInteractions: hoja lisa y una manilla de cada lado en la punta contraria a la
        // bisagra (la hoja de Revit traia el llavin donde fuera)
        float ancho = mitadHueco * 2f - 0.015f;
        float alto = altoHueco - 0.02f;
        Vector3 enElPlano = bisagra - abreHacia * 0.025f + cerrada * 0.005f;
        Material acero = Propio("Materiales", "Acero", new Color(0.7f, 0.72f, 0.74f), 0.6f, 0.3f);
        ParteDePuerta(puertaQueAbre.transform, "Hoja", enElPlano + cerrada * (ancho / 2f) + Vector3.up * (0.01f + alto / 2f), normal, new Vector3(ancho, alto, 0.045f), puerta.sharedMaterial);
        foreach (float frente in new float[] { 1f, -1f })
            ParteDePuerta(puertaQueAbre.transform, "Manilla", enElPlano + cerrada * (ancho - 0.12f) + Vector3.up * 1f + normal * frente * 0.07f, normal, new Vector3(0.16f, 0.04f, 0.1f), acero);

        // kinematic: la puerta solo gira cuando HingedDoor la mueve (igual que en HouseInteractions)
        Rigidbody cuerpo = puertaQueAbre.AddComponent<Rigidbody>();
        cuerpo.isKinematic = true;
        cuerpo.useGravity = false;
        cuerpo.interpolation = RigidbodyInterpolation.Interpolate;
        cuerpo.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        puertaQueAbre.AddComponent<HingedDoor>().maxOpenAngle = angulo;

        if (marco.Count == 0)
            Object.DestroyImmediate(puerta.gameObject);
    }

    // una pieza de la puerta (cubo como en CreatePart de HouseInteractions), de frente a la pared
    static void ParteDePuerta(Transform puerta, string nombre, Vector3 centro, Vector3 normal, Vector3 tamano, Material material)
    {
        GameObject parte = GameObject.CreatePrimitive(PrimitiveType.Cube);
        parte.name = nombre;
        parte.transform.SetParent(puerta, false);
        parte.transform.SetPositionAndRotation(centro, Quaternion.LookRotation(normal, Vector3.up));
        parte.transform.localScale = tamano;
        parte.GetComponent<Renderer>().sharedMaterial = material;
    }

    // a lo largo de la pared, el centro de las caras de la hoja (las que miran hacia los lados y estan a menos de
    // 3 cm del plano de la puerta; las del marco salen mas)
    static float CentroDeCaras(Vector3[] vertices, List<int> pegados, Vector3 normal, float plano, Vector3 largo, float siNoHay, out float ancho)
    {
        float desde = float.MaxValue;
        float hasta = float.MinValue;
        for (int i = 0; i < pegados.Count; i += 3)
        {
            Vector3 a = vertices[pegados[i]];
            Vector3 b = vertices[pegados[i + 1]];
            Vector3 c = vertices[pegados[i + 2]];
            if (Mathf.Abs(Vector3.Dot(Vector3.Cross(b - a, c - a).normalized, normal)) < 0.95f) continue;
            if (Mathf.Abs(Vector3.Dot(a, normal) - plano) > 0.03f) continue;
            foreach (Vector3 v in new Vector3[] { a, b, c })
            {
                desde = Mathf.Min(desde, Vector3.Dot(v, largo));
                hasta = Mathf.Max(hasta, Vector3.Dot(v, largo));
            }
        }
        ancho = desde <= hasta ? hasta - desde : 0f;
        return desde <= hasta ? (desde + hasta) / 2f : siNoHay;
    }

    // que tan lejos (en planta) esta el lavatorio mas cercano de ese punto
    static float Lavatorio(Vector3 punto, Transform casa)
    {
        float cerca = float.MaxValue;
        foreach (Renderer pieza in casa.GetComponentsInChildren<Renderer>())
        {
            if (!pieza.name.StartsWith("Pia") && !pieza.name.StartsWith("Mueble de lavabo")) continue;
            Vector3 c = pieza.bounds.ClosestPoint(new Vector3(punto.x, pieza.bounds.center.y, punto.z));
            cerca = Mathf.Min(cerca, Vector2.Distance(new Vector2(c.x, c.z), new Vector2(punto.x, punto.z)));
        }
        return cerca;
    }

    // de la bisagra hacia afuera, a lo largo de la pared: si lo primero es una pared cerca (la puerta abierta queda
    // recostada en ella) da mas puntos; si antes hay un mueble, el lavatorio o el ropero, da menos. una tele colgada
    // cuenta como la pared
    static float Recostada(Vector3 desde, Vector3 hacia, Transform casa)
    {
        float puntos = 0f;
        foreach (float alto in new float[] { 0.6f, 1.2f })
        {
            Vector3 origen = desde + Vector3.up * alto;
            RaycastHit[] hits = Physics.RaycastAll(origen, hacia, 3f);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            float pared = 3f;
            float antes = 3f;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.name.StartsWith("Muro"))
                {
                    pared = hit.distance;
                    break;
                }
                antes = Mathf.Min(antes, hit.distance);
            }
            // los muebles todavia no tienen collider: se revisan por su caja
            foreach (Renderer mueble in casa.GetComponentsInChildren<Renderer>())
            {
                float d;
                if (EsMueble(mueble.name) && mueble.bounds.IntersectRay(new Ray(origen, hacia), out d))
                    antes = Mathf.Min(antes, d);
            }
            puntos += antes < pared - 0.15f ? -10f : 3f - pared;
        }
        return puntos;
    }

    // ancho de la hoja (m, en Revit) segun el nombre de la puerta. si no lo dice, todo el ancho
    static float AnchoHoja(string nombre, float ancho)
    {
        Match medida = Regex.Match(nombre, @"(\d+) x 210 cm");
        if (medida.Success) return int.Parse(medida.Groups[1].Value) / 100f;
        if (nombre.Contains("1x2.10m")) return 1f;
        return ancho;
    }

    // cuanto piso se puede recorrer de ese lado de la puerta, en cuadros de 25 cm (hasta 1600). con la puerta
    // tapada, el lado del cuarto es el chico; el de la sala, el pasillo o afuera da mas. si dan igual, abre
    // para donde la dibujo Revit
    static int Espacio(Bounds puerta, Vector3 normal, float lado)
    {
        GameObject tapa = new GameObject("Tapa de puerta");
        tapa.transform.position = puerta.center;
        tapa.AddComponent<BoxCollider>().size = puerta.size + Vector3.one * 0.05f;
        Physics.SyncTransforms();

        Vector3 inicio = new Vector3(puerta.center.x, puerta.min.y + 1f, puerta.center.z) + normal * lado * (Vector3.Dot(puerta.extents, normal) + 0.3f);
        Queue<Vector2Int> cola = new Queue<Vector2Int>();
        HashSet<Vector2Int> vistos = new HashSet<Vector2Int> { Vector2Int.zero };
        cola.Enqueue(Vector2Int.zero);
        Vector2Int[] vecinos = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        while (cola.Count > 0 && vistos.Count < 1600)
        {
            Vector2Int celda = cola.Dequeue();
            Vector3 punto = inicio + new Vector3(celda.x, 0f, celda.y) * 0.25f;
            foreach (Vector2Int paso in vecinos)
            {
                Vector2Int otra = celda + paso;
                if (vistos.Contains(otra) || Physics.Linecast(punto, inicio + new Vector3(otra.x, 0f, otra.y) * 0.25f)) continue;
                vistos.Add(otra);
                cola.Enqueue(otra);
            }
        }

        Object.DestroyImmediate(tapa);
        Physics.SyncTransforms();
        return vistos.Count;
    }

    // si a los lados de la puerta (en la punta de cada jamba) hay pared
    static bool HayPared(Bounds puerta, Vector3 largo)
    {
        float mitad = Vector3.Dot(puerta.extents, largo) + 0.15f;
        foreach (float lado in new float[] { -1f, 1f })
        {
            Vector3 punto = new Vector3(puerta.center.x, puerta.min.y + 1f, puerta.center.z) + largo * lado * mitad;
            foreach (Collider otro in Physics.OverlapBox(punto, new Vector3(0.1f, 0.3f, 0.1f)))
            {
                if (otro.name.StartsWith("Muro")) return true;
            }
        }
        return false;
    }

    // cambia la malla de la pieza por la nueva (hecha en coordenadas de mundo alrededor de centro) y la pone
    // en centro, sin girar
    static void Reemplazar(Renderer pieza, Mesh malla, Vector3 centro, string tipo)
    {
        Guardar(malla, "Puertas", tipo + " " + Id(pieza.name));
        pieza.GetComponent<MeshFilter>().sharedMesh = malla;
        pieza.transform.SetPositionAndRotation(centro, Quaternion.identity);
        pieza.transform.localScale = Vector3.one;
        pieza.transform.localScale = new Vector3(1f / pieza.transform.lossyScale.x, 1f / pieza.transform.lossyScale.y, 1f / pieza.transform.lossyScale.z);
    }

    // cuantos grados (de 5 en 5, hasta 90) gira la hoja desde cerrada sin chocar con paredes ni muebles
    static float AnguloLibre(Vector3 bisagra, Vector3 haciaHoja, float signo, float anchoHoja, float alto, Renderer propia)
    {
        float libre = 0f;
        for (float angulo = 5f; angulo <= 90f; angulo += 5f)
        {
            Vector3 direccion = Quaternion.AngleAxis(signo * angulo, Vector3.up) * haciaHoja;
            // la caja se corre un poco hacia el hueco: abierta a 90 la hoja queda pegada al canto de la pared
            Vector3 haciaHueco = Vector3.Cross(Vector3.up, direccion);
            if (Vector3.Dot(haciaHueco, haciaHoja) < 0f)
                haciaHueco = -haciaHueco;
            Vector3 centro = bisagra + direccion * (anchoHoja / 2f + 0.03f) + haciaHueco * 0.05f + Vector3.up * (alto / 2f + 0.03f);
            Vector3 mitad = new Vector3(anchoHoja / 2f - 0.06f, alto / 2f - 0.08f, 0.035f);
            Quaternion giro = Quaternion.LookRotation(Vector3.Cross(direccion, Vector3.up), Vector3.up);

            foreach (Collider otro in Physics.OverlapBox(centro, mitad, giro))
            {
                if (otro.gameObject != propia.gameObject) return libre;
            }
            // los muebles todavia no tienen collider: se revisan por su caja
            foreach (Renderer mueble in propia.transform.root.GetComponentsInChildren<Renderer>())
            {
                if (EsMueble(mueble.name) && mueble.bounds.Intersects(new Bounds(centro, mitad * 2f))) return libre;
            }
            libre = angulo;
        }
        return libre;
    }

    // ---------- muebles ----------

    // como MakeMovable de HouseInteractions: se agarra con Grip y se le cambia el acabado con el trigger
    static void ArmarMueble(Renderer mueble, RadialMenu menu, Material[] acabados)
    {
        GameObject objeto = mueble.gameObject;
        Bounds local = objeto.GetComponent<MeshFilter>().sharedMesh.bounds;

        // la caja llega hasta lo que hay en el centro del mueble (el colchon, el asiento, la tapa): hasta arriba
        // de todo uno quedaba flotando sobre la cama, a la altura de la cabecera
        MeshCollider forma = objeto.AddComponent<MeshCollider>();
        Physics.SyncTransforms();
        RaycastHit hit;
        if (forma.Raycast(new Ray(mueble.bounds.center + Vector3.up * 5f, Vector3.down), out hit, 10f))
        {
            Vector3 arriba = objeto.transform.InverseTransformDirection(Vector3.up);
            int eje = Mathf.Abs(arriba.x) > 0.7f ? 0 : Mathf.Abs(arriba.y) > 0.7f ? 1 : 2;
            float tope = objeto.transform.InverseTransformPoint(hit.point)[eje];
            Vector3 min = local.min;
            Vector3 max = local.max;
            if (arriba[eje] > 0f) max[eje] = tope;
            else min[eje] = tope;
            local.SetMinMax(min, max);
        }
        Object.DestroyImmediate(forma);

        // un poco mas chica que el mueble para que no arranque metida en la pared o el piso
        BoxCollider caja = objeto.AddComponent<BoxCollider>();
        caja.center = local.center;
        caja.size = local.size - Vector3.one * 0.02f / Mathf.Max(objeto.transform.lossyScale.x, 0.001f);
        caja.sharedMaterial = Resbaloso();

        MaterialChanger acabado;
        // el escritorio va colgado de la pared (Revit no le trae patas): se le cambia el acabado pero no se mueve,
        // asi no lo empuja la mesa de vidrio que va debajo
        if (mueble.name.StartsWith("Estaci"))
        {
            objeto.AddComponent<XRSimpleInteractable>();
            acabado = objeto.AddComponent<MaterialChanger>();
            acabado.radialMenuPrefab = menu;
            acabado.materials = acabados;
            acabado.targetRenderer = mueble;
            acabado.allowRestoreOriginal = true;
            return;
        }

        // no se vuelca (rotacion x y z congeladas) y no sale volando al soltarlo
        Rigidbody cuerpo = objeto.AddComponent<Rigidbody>();
        cuerpo.mass = 8f;
        cuerpo.interpolation = RigidbodyInterpolation.Interpolate;
        cuerpo.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        cuerpo.maxLinearVelocity = 4f;
        cuerpo.maxAngularVelocity = 3f;
        cuerpo.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        XRGrabInteractable agarre = objeto.AddComponent<XRGrabInteractable>();
        agarre.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        agarre.limitLinearVelocity = true;
        agarre.maxLinearVelocityDelta = 4f;
        agarre.limitAngularVelocity = true;
        agarre.maxAngularVelocityDelta = 3f;
        agarre.useDynamicAttach = true;
        agarre.throwOnDetach = false;

        acabado = objeto.AddComponent<MaterialChanger>();
        acabado.radialMenuPrefab = menu;
        acabado.materials = acabados;
        acabado.targetRenderer = mueble;
        acabado.allowRestoreOriginal = true;
    }

    // poca friccion con el piso: con la de siempre el mueble se trababa al arrastrarlo
    static PhysicsMaterial Resbaloso()
    {
        string ruta = carpeta + "/Materiales/Mueble resbaloso.physicMaterial";
        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ruta);
        if (material == null)
        {
            material = new PhysicsMaterial("Mueble resbaloso");
            material.dynamicFriction = 0.15f;
            material.staticFriction = 0.15f;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
            AssetDatabase.CreateAsset(material, ruta);
        }
        return material;
    }

    // el plano se agranda pero los muebles no: cada uno vuelve a su tamano, pegado a las paredes que tenga cerca
    // (en x y en z por aparte: la cama queda con la cabecera contra la pared aunque tambien tenga una al lado)
    static void TamanoReal(Renderer mueble)
    {
        Bounds b = mueble.bounds;
        Vector3 ancla = new Vector3(b.center.x, b.min.y, b.center.z);
        foreach (Vector3 eje in new Vector3[] { Vector3.right, Vector3.forward })
        {
            float cerca = float.MaxValue;
            foreach (float signo in new float[] { 1f, -1f })
            {
                float mitad = Vector3.Dot(b.extents, eje);
                RaycastHit hit;
                if (Physics.Raycast(new Vector3(b.center.x, b.min.y + 0.5f, b.center.z), eje * signo, out hit, mitad + 0.4f) && hit.collider.name.StartsWith("Muro") && hit.distance < cerca)
                {
                    cerca = hit.distance;
                    ancla += eje * (Vector3.Dot(b.center, eje) + signo * mitad - Vector3.Dot(ancla, eje));
                }
            }
        }
        Transform t = mueble.transform;
        t.position = ancla + (t.position - ancla) / escala;
        t.localScale /= escala;
    }

    // la campana de la cocina quedaba flotando: se quita lo que esta arriba del mueble (mas de 1.4 m)
    static void SinCampana(Renderer cocina)
    {
        Transform t = cocina.transform;
        Mesh original = cocina.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = original.vertices;
        Vector3[] normales = original.normals;
        int[] triangulos = original.triangles;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = t.TransformPoint(vertices[i]);
            normales[i] = t.TransformDirection(normales[i]).normalized;
        }

        float tope = cocina.bounds.min.y + 1.4f;
        List<int> quedan = new List<int>();
        for (int i = 0; i < triangulos.Length; i += 3)
        {
            if (vertices[triangulos[i]].y > tope && vertices[triangulos[i + 1]].y > tope && vertices[triangulos[i + 2]].y > tope) continue;
            quedan.AddRange(new int[] { triangulos[i], triangulos[i + 1], triangulos[i + 2] });
        }

        Vector3 centro = cocina.bounds.center;
        Mesh malla = Malla(vertices, normales, quedan, centro);
        Guardar(malla, "Pisos", "Cocina " + Id(cocina.name));
        cocina.GetComponent<MeshFilter>().sharedMesh = malla;
        cocina.GetComponent<MeshCollider>().sharedMesh = malla;
        t.SetPositionAndRotation(centro, Quaternion.identity);
        t.localScale = Vector3.one;
        t.localScale = new Vector3(1f / t.lossyScale.x, 1f / t.lossyScale.y, 1f / t.lossyScale.z);
    }

    // cada mueble con dos materiales: el que cambia con los acabados (slot 0) y el de sus otras piezas (la ropa
    // de la cama, las patas del sillon, las manijas del ropero), segun que piezas tienen mucho detalle
    static void PartesDelMueble(Renderer mueble)
    {
        string n = mueble.name;
        Material madera = Propio("Materiales", "Madera", new Color(0.45f, 0.28f, 0.15f), 0.3f, 0f);
        Material acero = Propio("Materiales", "Acero", new Color(0.7f, 0.72f, 0.74f), 0.6f, 0.3f);
        Material negro = Propio("Materiales", "Plastico negro", new Color(0.06f, 0.06f, 0.065f), 0.35f, 0f);
        HashSet<int> detalladas = Detalladas(mueble);

        if (n.StartsWith("CAMA") || n.StartsWith("Bed_"))
            DosMateriales(mueble, (i, c, v) => !detalladas.Contains(i), madera, Propio("Materiales", "Ropa de cama", new Color(0.9f, 0.89f, 0.86f), 0.1f, 0f), "Muebles");
        else if (n.StartsWith("Guarda"))
            DosMateriales(mueble, (i, c, v) => !detalladas.Contains(i), madera, acero, "Muebles");
        else if (n.StartsWith("Estaci"))
            DosMateriales(mueble, (i, c, v) => !detalladas.Contains(i), Propio("Materiales", "Madera clara", new Color(0.72f, 0.57f, 0.4f), 0.35f, 0f), negro, "Muebles");
        else if (n.StartsWith("green office chair"))
            DosMateriales(mueble, (i, c, v) => detalladas.Contains(i), Propio("Materiales", "Tela verde", new Color(0.18f, 0.38f, 0.24f), 0.15f, 0f), negro, "Muebles");
        else if (n.StartsWith("Sof"))
            DosMateriales(mueble, (i, c, v) => detalladas.Contains(i), Propio("Materiales", "Cuero negro", new Color(0.07f, 0.07f, 0.075f), 0.55f, 0f), acero, "Muebles");
        else if (n.StartsWith("modern_center_table"))
            DosMateriales(mueble, (i, c, v) => detalladas.Contains(i), madera, acero, "Muebles");
    }

    // acabados segun el mueble: roperos en laminados, sillon y silla en tapices, lo demas en maderas (los 4 que
    // HouseInteractions crea para la mesita y el estante)
    static Material[] AcabadosPara(string mueble)
    {
        if (mueble.StartsWith("Guarda"))
            return new Material[]
            {
                Propio("Acabados", "Roble claro", new Color(0.7f, 0.55f, 0.36f), 0.3f, 0f),
                Propio("Acabados", "Grafito", new Color(0.2f, 0.21f, 0.22f), 0.4f, 0f),
                Propio("Acabados", "Blanco brillante", new Color(0.93f, 0.93f, 0.91f), 0.7f, 0f),
                Propio("Acabados", "Nogal", new Color(0.3f, 0.18f, 0.1f), 0.35f, 0f)
            };
        if (mueble.StartsWith("Sof") || mueble.StartsWith("green office chair"))
            return new Material[]
            {
                Propio("Acabados", "Cuero cafe", new Color(0.32f, 0.18f, 0.1f), 0.5f, 0f),
                Propio("Acabados", "Tela gris", new Color(0.45f, 0.46f, 0.47f), 0.1f, 0f),
                Propio("Acabados", "Tela azul", new Color(0.13f, 0.2f, 0.36f), 0.1f, 0f),
                Propio("Acabados", "Cuero negro", new Color(0.05f, 0.05f, 0.055f), 0.55f, 0f)
            };
        return new Material[]
        {
            Propio("Acabados", "Natural mate", new Color(0.42f, 0.23f, 0.11f), 0.2f, 0f),
            Propio("Acabados", "Oscuro satinado", new Color(0.12f, 0.055f, 0.025f), 0.5f, 0f),
            Propio("Acabados", "Blanco mate", new Color(0.86f, 0.84f, 0.79f), 0.15f, 0f),
            Propio("Acabados", "Negro satinado", new Color(0.035f, 0.04f, 0.045f), 0.45f, 0f)
        };
    }

    // ---------- materiales ----------

    // Revit no trae materiales: la casa usa los de la escena vieja (las paredes en Repello marfil, que es lo que
    // WallSides reconoce como pared) y lo de adentro, el lote y los carros un color propio
    static Material MaterialPara(string nombre)
    {
        if (nombre.StartsWith("Muro") || nombre.StartsWith("Canto")) return DeLaEscena("Repello marfil");
        if (nombre.StartsWith("Suelo piso porcelanato")) return DeLaEscena("Piso gris claro");
        if (nombre.StartsWith("Cubierta")) return DeLaEscena("Cubierta arena");
        if (nombre.StartsWith("Windows_") || nombre.StartsWith("Aluprof_MB-45") || nombre.StartsWith("Table-Coffee-Glass") || nombre.StartsWith("Sanitary_Shower")) return DeLaEscena("Vidrio celeste");
        if (nombre.StartsWith("Barandilla") || nombre.StartsWith("Tipo de barandal") || nombre.StartsWith("Poste") || EsPuerta(nombre)) return DeLaEscena("Marcos y barandas");

        if (nombre.StartsWith("Suelo cesped")) return Zacate();
        if (nombre.StartsWith("Suelo asfalto")) return Propio("Materiales", "Asfalto", new Color(0.16f, 0.16f, 0.17f), 0.15f, 0f);
        if (nombre.StartsWith("Suelo") || nombre.StartsWith("Escalera") || nombre.StartsWith("Tramo") || nombre.StartsWith("Blocks_Jardinera"))
            return Propio("Materiales", "Concreto exterior", new Color(0.52f, 0.52f, 0.5f), 0.1f, 0f);

        if (nombre.StartsWith("CAMA") || nombre.StartsWith("Bed_") || nombre.StartsWith("Guarda") || nombre.StartsWith("modern_center_table") || nombre.StartsWith("Mueble de lavabo"))
            return Propio("Materiales", "Madera", new Color(0.45f, 0.28f, 0.15f), 0.3f, 0f);
        if (nombre.StartsWith("Cocina") || nombre.StartsWith("Estaci"))
            return Propio("Materiales", "Madera clara", new Color(0.72f, 0.57f, 0.4f), 0.35f, 0f);
        if (nombre.StartsWith("Sof") || nombre.StartsWith("Taburete"))
            return Propio("Materiales", "Cuero negro", new Color(0.07f, 0.07f, 0.075f), 0.55f, 0f);
        if (nombre.StartsWith("green office chair"))
            return Propio("Materiales", "Tela verde", new Color(0.18f, 0.38f, 0.24f), 0.15f, 0f);
        if (nombre.StartsWith("WC") || nombre.StartsWith("Pia") || nombre.StartsWith("Lavadora"))
            return Propio("Materiales", "Ceramica blanca", new Color(0.94f, 0.94f, 0.93f), 0.85f, 0f);
        if (nombre.StartsWith("Refrigerator") || nombre.StartsWith("Shower Head") || nombre.StartsWith("archiclassic"))
            return Propio("Materiales", "Acero", new Color(0.7f, 0.72f, 0.74f), 0.6f, 0.3f);
        if (nombre.StartsWith("18039_Coniferous"))
            return Propio("Materiales", "Pino", new Color(0.1f, 0.28f, 0.12f), 0.1f, 0f);
        if (nombre.StartsWith("Audi_TT"))
            return Propio("Materiales", "Carro azul", new Color(0.08f, 0.14f, 0.32f), 0.8f, 0.6f);
        if (nombre.StartsWith("Audi_"))
            return Propio("Materiales", "Carro gris", new Color(0.45f, 0.46f, 0.48f), 0.8f, 0.6f);

        return DeLaEscena("Paredes interiores");
    }

    // material de Assets/Materiales (los de la escena vieja)
    static Material DeLaEscena(string nombre)
    {
        return AssetDatabase.LoadAssetAtPath<Material>("Assets/Materiales/" + nombre + ".mat");
    }

    // material del plano nuevo (Plano nuevo/<subcarpeta>): copia de Piso gris claro con otro color y con las dos
    // caras, porque Revit trae caras de un solo lado y si no se ven huecos (como en los carros)
    static Material Propio(string subcarpeta, string nombre, Color color, float brillo, float metal)
    {
        if (!AssetDatabase.IsValidFolder(carpeta + "/" + subcarpeta))
            AssetDatabase.CreateFolder(carpeta, subcarpeta);

        string ruta = carpeta + "/" + subcarpeta + "/" + nombre + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            material = new Material(DeLaEscena("Piso gris claro"));
            AssetDatabase.CreateAsset(material, ruta);
        }
        material.color = color;
        material.SetFloat("_Smoothness", brillo);
        material.SetFloat("_Metallic", metal);
        // 0: se dibujan las dos caras
        material.SetFloat("_Cull", 0f);
        material.doubleSidedGI = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    // el zacate del lote con la misma textura del zacate del terreno, para que amarre con lo de afuera
    static Material Zacate()
    {
        Material material = Propio("Materiales", "Zacate", Color.white, 0f, 0f);
        foreach (TerrainLayer capa in AssetDatabase.LoadAssetAtPath<TerrainData>(terrenoMuestra).terrainLayers)
        {
            if (capa == null || !capa.name.Contains("Grass")) continue;

            material.mainTexture = capa.diffuseTexture;
            // los UV del zacate de Revit van ~4.65 unidades por metro; asi se repite igual que en el terreno
            material.mainTextureScale = Vector2.one / (capa.tileSize.x * 4.65f);
        }
        return material;
    }

    // ---------- brochas y luces (casa 1) ----------

    // como en el apartamento: una brocha en el piso de cada planta (con el menu en el suelo) y brochas de pared en
    // la planta baja (una por cuarto). WallSides decide al empezar que lado pinta cada una segun de que lado esta
    static int ArmarBrochas(Transform casa, List<Renderer> tramos, MaterialChanger brochaPiso, MaterialChanger brochaPared)
    {
        GameObject grupo = new GameObject("Brochas casa 1");
        // los menus de acabados de los muebles no se meten en paredes ni muebles
        grupo.AddComponent<MenuLibre>();
        int hechas = 0;

        foreach (Renderer losa in casa.GetComponentsInChildren<Renderer>())
        {
            if (!losa.name.StartsWith("Suelo piso porcelanato")) continue;

            Vector3 punto = PuntoLibre(losa.bounds);
            MaterialChanger piso = Object.Instantiate(brochaPiso, grupo.transform);
            piso.name = "Brocha piso " + Planta(losa.bounds);
            piso.transform.position = new Vector3(punto.x, losa.bounds.max.y + 0.04f, punto.z);
            piso.targetRenderer = losa;
            List<Renderer> umbrales = new List<Renderer>();
            foreach (Renderer umbral in casa.GetComponentsInChildren<Renderer>())
            {
                if (umbral.name.StartsWith("Suelo umbral") && Planta(umbral.bounds) == Planta(losa.bounds))
                    umbrales.Add(umbral);
            }
            piso.additionalRenderers = umbrales.ToArray();
            piso.menuOnFloor = true;
            piso.materialSlot = 0;
            hechas++;
        }

        // paredes de la planta baja. Blender ya corto las paredes donde les llega otra, asi cada lado de cada pieza
        // da a un solo espacio. los cuatro cuartos (lavanderia, bano y los dos cuartos) llevan una brocha cada uno; la
        // sala, cocina y pasillo (el espacio mas grande) una por pared; y afuera una sola para la fachada, entre las
        // ventanas del frente. cada brocha pinta su pared y PintarCuarto copia el acabado al resto de su grupo
        List<Cuarto> cuartos = Cuartos(tramos, huecosDePuertas, casa);
        Cuarto comun = null;
        foreach (Cuarto cuarto in cuartos)
        {
            if (!cuarto.afuera && (comun == null || cuarto.cuadros > comun.cuadros))
                comun = cuarto;
        }
        List<Cuarto> adentro = cuartos.FindAll(c => !c.afuera && c != comun);
        adentro.Sort((x, y) => y.cuadros.CompareTo(x.cuadros));
        for (int k = 4; k < adentro.Count; k++)
        {
            for (int q = 0; q < adentro[k].paredes.Count; q++)
                comun.Agregar(adentro[k].paredes[q], adentro[k].lados[q], adentro[k].hacia[q]);
            cuartos.Remove(adentro[k]);
        }
        // las vigas (lo que queda de pared arriba de las puertas, desde 2.6 m) no las ve el recorrido, que va a 1.3 m:
        // cada cara va con el espacio al que da
        List<Renderer> vigas = tramos.FindAll(t => Planta(t.bounds) == "baja" && t.bounds.min.y > centroCasa.y + 2f);
        foreach (Renderer viga in vigas)
        {
            foreach (Vector3 hacia in new Vector3[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                int lado = SlotDe(viga, hacia);
                if (lado < 0) continue;
                Vector3 enfrente = viga.bounds.center + hacia * (Vector3.Dot(viga.bounds.extents, Abs(hacia)) + 0.4f);
                Cuarto suyo = cuartos.Find(c => c.celdas.Contains(Celda(enfrente)));
                if (suyo != null)
                    suyo.Agregar(viga, lado, suyo == comun ? hacia : Vector3.zero);
            }
        }

        // los cantos (marcos de puertas y ventanas, puntas de pared) vienen aparte desde Blender, uno por direccion.
        // cada uno va con el espacio al que da; en un marco de puerta o ventana da a dos, y va con el cuarto (antes que
        // con la sala, y antes que con afuera)
        foreach (Renderer canto in casa.GetComponentsInChildren<Renderer>())
        {
            if (!canto.name.StartsWith("Canto") || Planta(canto.bounds) != "baja") continue;
            Mesh malla = canto.GetComponent<MeshFilter>().sharedMesh;
            Vector3 n = canto.transform.TransformDirection(malla.normals[malla.triangles[0]]).normalized;
            Cuarto suyo = DelCanto(canto.bounds.center, n, cuartos, comun);
            if (suyo != null)
                suyo.Agregar(canto, 0, Vector3.zero);
        }
        // igual el costado de una punta de pared que queda dentro del hueco de una puerta (la jamba derecha del bano):
        // el recorrido no lo ve porque la puerta esta tapada, y quedaba sin brocha
        foreach (Renderer tramo in tramos)
        {
            if (Planta(tramo.bounds) != "baja") continue;
            foreach (Vector3 hacia in new Vector3[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                int lado = SlotDe(tramo, hacia);
                Vector3 enfrente = tramo.bounds.center + hacia * (Vector3.Dot(tramo.bounds.extents, Abs(hacia)) + 0.1f);
                if (lado < 0 || cuartos.Exists(c => c.Tiene(tramo, lado)) || !huecosDePuertas.Exists(h => h.Contains(new Vector3(enfrente.x, h.center.y, enfrente.z)))) continue;
                // desde un poco enfrente de la cara: a los lados del centro de la pieza solo esta la pared que le llega
                Cuarto suyo = DelCanto(enfrente + hacia * 0.05f, hacia, cuartos, comun);
                if (suyo != null)
                    suyo.Agregar(tramo, lado, Vector3.zero);
            }
        }

        // el pasillo de las tres puertas (bano, cuarto con escritorio y cuarto sin escritorio): todas las caras que
        // dan al pasillo cerca de esa puerta salen de la sala y van con una sola brocha encima de la puerta del cuarto
        // sin escritorio
        Bounds puertaSinEscritorio;
        Panel sobrePuerta = SobreLaPuerta(cuartos, comun, vigas, casa, out puertaSinEscritorio);
        if (sobrePuerta != null)
        {
            Cuarto union = new Cuarto();
            Vector3 centroUnion = new Vector3(puertaSinEscritorio.center.x, 0f, puertaSinEscritorio.center.z);
            for (int k = comun.paredes.Count - 1; k >= 0; k--)
            {
                Bounds b = comun.paredes[k].bounds;
                Vector3 cerca = b.ClosestPoint(new Vector3(centroUnion.x, b.center.y, centroUnion.z));
                if (Vector3.Distance(new Vector3(cerca.x, 0f, cerca.z), centroUnion) > 1.6f) continue;
                union.Agregar(comun.paredes[k], comun.lados[k], comun.hacia[k]);
                comun.paredes.RemoveAt(k);
                comun.lados.RemoveAt(k);
                comun.hacia.RemoveAt(k);
            }
            if (union.paredes.Count > 0)
                hechas += PonerBrocha(union, "Brocha union ", tramos, brochaPared, grupo.transform, sobrePuerta) ? 1 : 0;
        }

        // afuera tambien va la orilla de las losas (slot 1: solo la orilla de afuera, la franja entre los pisos)
        foreach (Cuarto cuarto in cuartos)
        {
            if (!cuarto.afuera) continue;
            foreach (Renderer losa in casa.GetComponentsInChildren<Renderer>())
            {
                if (losa.name.StartsWith("Suelo piso porcelanato") && losa.sharedMaterials.Length == 3)
                    cuarto.Agregar(losa, 1, Vector3.zero);
            }
        }

        foreach (Cuarto cuarto in cuartos)
        {
            if (cuarto == comun)
            {
                foreach (Cuarto pared in PorPared(cuarto))
                    hechas += PonerBrocha(pared, "Brocha sala ", tramos, brochaPared, grupo.transform, null) ? 1 : 0;
            }
            else if (cuarto.afuera)
                hechas += PonerBrocha(cuarto, "Brocha fachada ", tramos, brochaPared, grupo.transform, Frente(cuarto, casa)) ? 1 : 0;
            else
                hechas += PonerBrocha(cuarto, "Brocha cuarto ", tramos, brochaPared, grupo.transform, null) ? 1 : 0;
        }

        // las de modelo ya no hacen falta
        Object.DestroyImmediate(brochaPiso.gameObject);
        Object.DestroyImmediate(brochaPared.gameObject);
        return hechas;
    }

    // si WallSides va a poder partir el tramo en dos lados: su lado mas delgado es horizontal y tiene caras
    // hacia los dos lados (las astillas de las esquinas no, se quedan con un solo material)
    static bool Partible(Renderer tramo)
    {
        Mesh malla = tramo.GetComponent<MeshFilter>().sharedMesh;
        Vector3 tamano = malla.bounds.size;
        if (tamano.y <= tamano.x && tamano.y <= tamano.z) return false;
        // los pilares casi cuadrados no tienen frente y espalda claros: WallSides los parte por cualquier lado
        if (Mathf.Max(tamano.x, tamano.z) < 1.5f * Mathf.Min(tamano.x, tamano.z)) return false;

        Vector3 eje = tamano.x <= tamano.z ? Vector3.right : Vector3.forward;
        bool adelante = false;
        bool atras = false;
        foreach (Vector3 normal in malla.normals)
        {
            float d = Vector3.Dot(normal, eje);
            if (d > 0.5f) adelante = true;
            if (d < -0.5f) atras = true;
        }
        return adelante && atras;
    }

    class Panel
    {
        public Vector3 hacia;
        public float cara;
        public float desde;
        public float hasta;
        public float alto;
        public List<Renderer> tramos = new List<Renderer>();
    }

    // la esquina de la cuadricula de los cuartos (cuadros de 25 cm)
    static Vector3 origenCuadros;

    static Vector2Int Celda(Vector3 punto)
    {
        return new Vector2Int(Mathf.FloorToInt((punto.x - origenCuadros.x) / 0.25f), Mathf.FloorToInt((punto.z - origenCuadros.z) / 0.25f));
    }

    // un cuarto de la planta baja: sus paredes, el slot del lado de cada una que da hacia el y hacia donde mira
    class Cuarto
    {
        public int cuadros;
        public HashSet<Vector2Int> celdas = new HashSet<Vector2Int>();
        public List<Renderer> paredes = new List<Renderer>();
        public List<int> lados = new List<int>();
        public List<Vector3> hacia = new List<Vector3>();
        public bool afuera;

        public bool Tiene(Renderer pared, int lado)
        {
            for (int k = 0; k < paredes.Count; k++)
                if (paredes[k] == pared && lados[k] == lado) return true;
            return false;
        }

        public void Agregar(Renderer pared, int lado, Vector3 mira)
        {
            if (Tiene(pared, lado)) return;
            paredes.Add(pared);
            lados.Add(lado);
            hacia.Add(mira);
        }
    }

    // los cuartos de la planta baja: se recorre el piso en cuadros de 25 cm (a 1.3 m, a media ventana) sin cruzar
    // paredes, puertas ni ventanas. el que llega a la orilla del recorrido es la fachada (afuera)
    static List<Cuarto> Cuartos(List<Renderer> tramos, List<Bounds> puertas, Transform casa)
    {
        List<Renderer> abajo = tramos.FindAll(t => Planta(t.bounds) == "baja");

        // mientras se recorre, cada pared es una caja solida de su tamano: tapa sus puertas y ventanas, y las uniones
        // entre piezas no dejan rendijas por donde se cuele el recorrido de un cuarto a otro
        Dictionary<Collider, Renderer> cajas = new Dictionary<Collider, Renderer>();
        foreach (Renderer pared in abajo)
        {
            GameObject tapa = new GameObject("Tapa");
            tapa.transform.position = pared.bounds.center;
            BoxCollider caja = tapa.AddComponent<BoxCollider>();
            caja.size = pared.bounds.size;
            cajas[caja] = pared;
        }
        // y las puertas (algunas quedan entre dos paredes y no dentro de una)
        List<GameObject> tapas = new List<GameObject>();
        foreach (Bounds puerta in puertas)
        {
            GameObject tapa = new GameObject("Tapa");
            tapa.transform.position = puerta.center;
            tapa.AddComponent<BoxCollider>().size = new Vector3(puerta.size.x > puerta.size.z ? puerta.size.x : 0.3f, puerta.size.y, puerta.size.z > puerta.size.x ? puerta.size.z : 0.3f);
            tapas.Add(tapa);
        }
        Physics.SyncTransforms();
        Bounds planta = Caja(abajo.ToArray());
        planta.Expand(new Vector3(3f, 0f, 3f));
        origenCuadros = planta.min;
        int nx = Mathf.CeilToInt(planta.size.x / 0.25f);
        int nz = Mathf.CeilToInt(planta.size.z / 0.25f);
        System.Func<int, int, Vector3> centro = (a, b) => new Vector3(planta.min.x + (a + 0.5f) * 0.25f, planta.min.y + 1.3f, planta.min.z + (b + 0.5f) * 0.25f);
        bool[,] visto = new bool[nx, nz];
        Dictionary<KeyValuePair<Renderer, int>, Dictionary<Cuarto, int>> cubos = new Dictionary<KeyValuePair<Renderer, int>, Dictionary<Cuarto, int>>();
        Vector2Int[] pasos = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        List<Cuarto> cuartos = new List<Cuarto>();
        for (int a0 = 0; a0 < nx; a0++)
        {
            for (int b0 = 0; b0 < nz; b0++)
            {
                // los cuadros que caen adentro de una pared no son de ningun cuarto
                Renderer adentro;
                if (visto[a0, b0] || (Tapado(centro(a0, b0), Vector3.zero, centro(a0, b0), cajas, out adentro) && adentro != null)) continue;
                Cuarto cuarto = new Cuarto();
                int cuadros = 0;
                Queue<Vector2Int> cola = new Queue<Vector2Int>();
                cola.Enqueue(new Vector2Int(a0, b0));
                visto[a0, b0] = true;
                while (cola.Count > 0)
                {
                    Vector2Int c = cola.Dequeue();
                    cuadros++;
                    cuarto.celdas.Add(c);
                    foreach (Vector2Int paso in pasos)
                    {
                        Vector2Int o = c + paso;
                        if (o.x < 0 || o.y < 0 || o.x >= nx || o.y >= nz)
                        {
                            cuarto.afuera = true;
                            continue;
                        }
                        // la pared que hay entre este cuadro y el de al lado (o en el de al lado): su cara mira hacia aca
                        Vector3 haciaOtro = new Vector3(paso.x, 0f, paso.y);
                        Renderer pared;
                        if (Tapado(centro(c.x, c.y), haciaOtro, centro(o.x, o.y), cajas, out pared))
                        {
                            // los cubitos de 25 cm donde se juntan paredes no tienen frente y espalda: se anotan aparte
                            if (pared != null && !Partible(pared))
                            {
                                // WallSides deja los cantos en el slot 0
                                int cara = Mathf.Max(SlotDe(pared, -haciaOtro), 0);
                                KeyValuePair<Renderer, int> clave = new KeyValuePair<Renderer, int>(pared, cara);
                                if (!cubos.ContainsKey(clave)) cubos[clave] = new Dictionary<Cuarto, int>();
                                int votos;
                                cubos[clave].TryGetValue(cuarto, out votos);
                                cubos[clave][cuarto] = votos + 1;
                                continue;
                            }
                            int lado = pared != null ? SlotDe(pared, -haciaOtro) : -1;
                            if (lado >= 0) cuarto.Agregar(pared, lado, -haciaOtro);
                            continue;
                        }
                        if (visto[o.x, o.y]) continue;
                        visto[o.x, o.y] = true;
                        cola.Enqueue(o);
                    }
                }
                // los huequitos entre muebles y paredes (una sola pared) no son cuartos
                cuarto.cuadros = cuadros;
                if (cuarto.afuera || (cuadros >= 6 && cuarto.paredes.Count >= 2))
                    cuartos.Add(cuarto);
            }
        }
        // cada slot de un cubito va con el espacio al que dan mas de sus caras (si una cara da a un cuarto y la otra al
        // pasillo, cada una con el suyo: la franja dentro de un cuarto se pinta con ese cuarto)
        foreach (KeyValuePair<KeyValuePair<Renderer, int>, Dictionary<Cuarto, int>> cubo in cubos)
        {
            Cuarto suyo = null;
            foreach (KeyValuePair<Cuarto, int> votos in cubo.Value)
            {
                if (cuartos.Contains(votos.Key) && (suyo == null || votos.Value > cubo.Value[suyo]))
                    suyo = votos.Key;
            }
            if (suyo != null)
                suyo.Agregar(cubo.Key.Key, cubo.Key.Value, Vector3.zero);
        }

        foreach (Collider tapa in cajas.Keys)
            Object.DestroyImmediate(tapa.gameObject);
        foreach (GameObject tapa in tapas)
            Object.DestroyImmediate(tapa);
        Physics.SyncTransforms();
        return cuartos;
    }

    // si entre un cuadro y el de al lado hay pared (la que cruza el paso, o en la que cae el otro cuadro) o una
    // puerta. si es pared, cual
    static bool Tapado(Vector3 desde, Vector3 hacia, Vector3 otro, Dictionary<Collider, Renderer> cajas, out Renderer pared)
    {
        pared = null;
        bool puerta = false;
        float cerca = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(desde, hacia, 0.25f))
        {
            Renderer suya;
            if (cajas.TryGetValue(hit.collider, out suya) && hit.distance < cerca)
            {
                cerca = hit.distance;
                pared = suya;
            }
            puerta |= hit.collider.name == "Tapa";
        }
        if (pared != null) return true;
        foreach (Collider caja in Physics.OverlapBox(otro, Vector3.one * 0.01f))
        {
            if (cajas.TryGetValue(caja, out pared)) return true;
            puerta |= caja.name == "Tapa";
        }
        return puerta;
    }

    // la brocha del grupo: en la pared mas larga donde quepa (o, si se da, lo mas cerca posible de "cerca"). pinta
    // su pared y PintarCuarto copia el acabado a las demas del grupo
    static bool PonerBrocha(Cuarto cuarto, string nombre, List<Renderer> tramos, MaterialChanger modelo, Transform grupo, Panel cerca)
    {
        List<Panel> paneles = new List<Panel>();
        if (cerca != null)
            paneles.Add(cerca);
        List<int> orden = new List<int>();
        for (int k = 0; k < cuarto.paredes.Count; k++)
            if (cuarto.hacia[k] != Vector3.zero) orden.Add(k);
        orden.Sort((x, y) => Largo(cuarto.paredes[y], cuarto.hacia[y]).CompareTo(Largo(cuarto.paredes[x], cuarto.hacia[x])));
        foreach (int k in orden)
        {
            float cara, desde, hasta;
            if (!CaraPrincipal(cuarto.paredes[k], cuarto.hacia[k], out cara, out desde, out hasta)) continue;
            Panel panel = new Panel { hacia = cuarto.hacia[k], cara = cara, desde = desde, hasta = hasta, alto = cuarto.paredes[k].bounds.min.y };
            panel.tramos.Add(cuarto.paredes[k]);
            paneles.Add(panel);
        }

        foreach (Panel panel in paneles)
        {
            Vector3 punto;
            Renderer objetivo;
            if (!FrenteALaPared(panel, tramos, out punto, out objetivo)) continue;

            MaterialChanger brocha = Object.Instantiate(modelo, grupo);
            brocha.name = nombre + grupo.childCount;
            brocha.transform.SetPositionAndRotation(punto, Quaternion.LookRotation(-panel.hacia, Vector3.up));
            brocha.targetRenderer = objetivo;
            brocha.additionalRenderers = new Renderer[0];
            // el menu sale ahi mismo sobre la pared, en lugar de la brocha
            brocha.menuOnWall = true;
            PintarCuarto pintar = brocha.gameObject.AddComponent<PintarCuarto>();
            pintar.walls = cuarto.paredes.ToArray();
            pintar.slots = cuarto.lados.ToArray();
            return true;
        }
        return false;
    }

    // a que espacio le toca un canto que da a dos: primero un cuarto, despues la sala, despues afuera
    static int Prioridad(Cuarto cuarto, Cuarto comun)
    {
        return cuarto.afuera ? 0 : cuarto == comun ? 1 : 2;
    }

    // el espacio de un canto que mira hacia n: lo que hay enfrente y a sus costados, con la prioridad de arriba
    static Cuarto DelCanto(Vector3 centro, Vector3 n, List<Cuarto> cuartos, Cuarto comun)
    {
        List<Vector3> puntos = new List<Vector3> { centro + n * 0.35f };
        foreach (Vector3 lado in new Vector3[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            if (Mathf.Abs(Vector3.Dot(lado, n)) < 0.5f)
                puntos.Add(centro + lado * 0.5f);
        }
        Cuarto suyo = null;
        foreach (Vector3 punto in puntos)
        {
            Cuarto aca = cuartos.Find(c => c.celdas.Contains(Celda(punto)));
            if (aca != null && (suyo == null || Prioridad(aca, comun) > Prioridad(suyo, comun)))
                suyo = aca;
        }
        return suyo;
    }

    // la viga encima de la puerta del cuarto sin escritorio (el que no tiene escritorio, lavadora ni inodoro), del
    // lado del pasillo: ahi va la brocha de la union de las tres puertas
    static Panel SobreLaPuerta(List<Cuarto> cuartos, Cuarto comun, List<Renderer> vigas, Transform casa, out Bounds laPuerta)
    {
        laPuerta = new Bounds();
        Cuarto sinEscritorio = cuartos.Find(c => !c.afuera && c != comun && !System.Array.Exists(casa.GetComponentsInChildren<Renderer>(),
            r => (r.name.StartsWith("Estaci") || r.name.StartsWith("Lavadora") || r.name.StartsWith("WC")) && c.celdas.Contains(Celda(r.bounds.center))));
        if (sinEscritorio == null) return null;

        foreach (Bounds puerta in huecosDePuertas)
        {
            Vector3 normal = puerta.size.x < puerta.size.z ? Vector3.right : Vector3.forward;
            foreach (float lado in new float[] { 1f, -1f })
            {
                if (!sinEscritorio.celdas.Contains(Celda(puerta.center - normal * lado * 0.5f)) || !comun.celdas.Contains(Celda(puerta.center + normal * lado * 0.5f))) continue;

                Renderer viga = vigas.Find(v => v.bounds.min.x - 0.05f < puerta.center.x && puerta.center.x < v.bounds.max.x + 0.05f
                    && v.bounds.min.z - 0.05f < puerta.center.z && puerta.center.z < v.bounds.max.z + 0.05f);
                float cara, desde, hasta;
                if (viga == null || !CaraPrincipal(viga, normal * lado, out cara, out desde, out hasta)) return null;
                Panel panel = new Panel { hacia = normal * lado, cara = cara, desde = desde, hasta = hasta, alto = viga.bounds.min.y + 0.3f - 1.5f };
                panel.tramos.Add(viga);
                laPuerta = puerta;
                return panel;
            }
        }
        return null;
    }

    // la sala, la cocina y el pasillo: un grupo por pared (las piezas del mismo plano que se tocan). las piezas de
    // menos de 80 cm (la punta de una pared, el canto junto a una puerta) van con el grupo mas cercano
    static List<Cuarto> PorPared(Cuarto cuarto)
    {
        List<Cuarto> grupos = new List<Cuarto>();
        List<Panel> planos = new List<Panel>();
        for (int k = 0; k < cuarto.paredes.Count; k++)
        {
            Renderer pared = cuarto.paredes[k];
            float cara, desde, hasta;
            if (cuarto.hacia[k] == Vector3.zero || !CaraPrincipal(pared, cuarto.hacia[k], out cara, out desde, out hasta)) continue;
            int g = planos.FindIndex(x => x.hacia == cuarto.hacia[k] && Mathf.Abs(x.cara - cara) < 0.03f && desde < x.hasta + 0.05f && x.desde < hasta + 0.05f);
            if (g < 0)
            {
                planos.Add(new Panel { hacia = cuarto.hacia[k], cara = cara, desde = desde, hasta = hasta });
                grupos.Add(new Cuarto());
                g = grupos.Count - 1;
            }
            planos[g].desde = Mathf.Min(planos[g].desde, desde);
            planos[g].hasta = Mathf.Max(planos[g].hasta, hasta);
            grupos[g].Agregar(pared, cuarto.lados[k], cuarto.hacia[k]);
        }

        // los cubitos y lo que no tiene direccion: con el grupo mas cercano
        for (int k = 0; k < cuarto.paredes.Count && grupos.Count > 0; k++)
        {
            if (cuarto.hacia[k] != Vector3.zero) continue;
            Vector3 centro = cuarto.paredes[k].bounds.center;
            centro.y = 0f;
            int cerca = 0;
            for (int h = 1; h < grupos.Count; h++)
            {
                if (Vector3.Distance(Centro(planos[h]), centro) < Vector3.Distance(Centro(planos[cerca]), centro))
                    cerca = h;
            }
            grupos[cerca].Agregar(cuarto.paredes[k], cuarto.lados[k], Vector3.zero);
        }

        for (int g = grupos.Count - 1; g >= 0; g--)
        {
            if (planos[g].hasta - planos[g].desde >= 0.8f || grupos.Count == 1) continue;
            Vector3 centro = Centro(planos[g]);
            int cerca = -1;
            for (int h = 0; h < grupos.Count; h++)
            {
                if (h != g && (cerca < 0 || Vector3.Distance(Centro(planos[h]), centro) < Vector3.Distance(Centro(planos[cerca]), centro)))
                    cerca = h;
            }
            for (int k = 0; k < grupos[g].paredes.Count; k++)
                grupos[cerca].Agregar(grupos[g].paredes[k], grupos[g].lados[k], grupos[g].hacia[k]);
            grupos.RemoveAt(g);
            planos.RemoveAt(g);
        }
        return grupos;
    }

    static Vector3 Centro(Panel panel)
    {
        return Abs(Vector3.Cross(Vector3.up, panel.hacia)) * (panel.desde + panel.hasta) / 2f + Abs(panel.hacia) * panel.cara;
    }

    // la fachada del frente (la que da a los carros): su lado de afuera, buscando la brocha desde el medio de las
    // dos ventanas
    static Panel Frente(Cuarto afuera, Transform casa)
    {
        Vector3 carros = Vector3.zero;
        int cuantos = 0;
        foreach (Renderer carro in casa.root.GetComponentsInChildren<Renderer>())
        {
            if (!carro.name.StartsWith("Audi_")) continue;
            carros += carro.bounds.center;
            cuantos++;
        }
        if (cuantos == 0) return null;
        carros /= cuantos;

        Panel frente = null;
        float mejor = float.MaxValue;
        for (int k = 0; k < afuera.paredes.Count; k++)
        {
            Renderer pared = afuera.paredes[k];
            Vector3 hacia = afuera.hacia[k];
            float cara, desde, hasta;
            if (hacia == Vector3.zero || Vector3.Dot((carros - pared.bounds.center).normalized, hacia) < 0.5f || !CaraPrincipal(pared, hacia, out cara, out desde, out hasta)) continue;
            float lejos = Mathf.Abs(Vector3.Dot(carros, Abs(hacia)) - cara);
            if (frente == null || lejos < mejor - 0.05f)
            {
                frente = new Panel { hacia = hacia, cara = cara, desde = desde, hasta = hasta, alto = pared.bounds.min.y };
                mejor = lejos;
            }
            if (Mathf.Abs(lejos - mejor) <= 0.05f && Mathf.Abs(frente.cara - cara) < 0.05f)
            {
                frente.tramos.Add(pared);
                frente.desde = Mathf.Min(frente.desde, desde);
                frente.hasta = Mathf.Max(frente.hasta, hasta);
            }
        }
        if (frente == null) return null;

        // las dos ventanas de esa fachada en la planta baja
        Vector3 largo = Abs(Vector3.Cross(Vector3.up, frente.hacia));
        List<float> ventanas = new List<float>();
        foreach (Renderer ventana in casa.GetComponentsInChildren<Renderer>())
        {
            if ((ventana.name.StartsWith("Windows_") || ventana.name.StartsWith("Aluprof_MB-45")) && Planta(ventana.bounds) == "baja"
                && Mathf.Abs(Vector3.Dot(ventana.bounds.center, Abs(frente.hacia)) - frente.cara) < 0.5f)
                ventanas.Add(Vector3.Dot(ventana.bounds.center, largo));
        }
        if (ventanas.Count >= 2)
        {
            ventanas.Sort();
            float medio = (ventanas[0] + ventanas[1]) / 2f;
            float mitad = Mathf.Max(medio - frente.desde, frente.hasta - medio);
            frente.desde = medio - mitad;
            frente.hasta = medio + mitad;
        }
        return frente;
    }

    // largo de la cara de la pared que mira hacia "hacia"
    static float Largo(Renderer pared, Vector3 hacia)
    {
        return Vector3.Dot(pared.bounds.size, Abs(Vector3.Cross(Vector3.up, hacia)));
    }

    // en que slot deja WallSides el lado del tramo que mira hacia "hacia": el 0 es el que mira hacia + del mundo
    // en el eje mas delgado de la malla. -1 si ese lado no es ni el frente ni la espalda del tramo
    static int SlotDe(Renderer tramo, Vector3 hacia)
    {
        Vector3 tamano = tramo.GetComponent<MeshFilter>().sharedMesh.bounds.size;
        Vector3 eje = tramo.transform.TransformDirection(tamano.x <= tamano.y && tamano.x <= tamano.z ? Vector3.right : tamano.y <= tamano.z ? Vector3.up : Vector3.forward);
        float d = Vector3.Dot(hacia, Abs(eje));
        return Mathf.Abs(d) < 0.5f ? -1 : d > 0f ? 0 : 1;
    }

    // el plano con mas area de las caras del tramo que miran hacia "hacia": su posicion y de donde a donde va
    static bool CaraPrincipal(Renderer tramo, Vector3 hacia, out float cara, out float desde, out float hasta)
    {
        Mesh malla = tramo.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = malla.vertices;
        int[] triangulos = malla.triangles;
        Vector3 largo = Abs(Vector3.Cross(Vector3.up, hacia));
        Dictionary<int, float> areas = new Dictionary<int, float>();
        for (int i = 0; i < triangulos.Length; i += 3)
        {
            Vector3 a = tramo.transform.TransformPoint(vertices[triangulos[i]]);
            Vector3 cruz = Vector3.Cross(tramo.transform.TransformPoint(vertices[triangulos[i + 1]]) - a, tramo.transform.TransformPoint(vertices[triangulos[i + 2]]) - a);
            if (Vector3.Dot(cruz.normalized, hacia) < 0.9f) continue;
            int clave = Mathf.RoundToInt(Vector3.Dot(a, Abs(hacia)) * 100f);
            float area;
            areas.TryGetValue(clave, out area);
            areas[clave] = area + cruz.magnitude / 2f;
        }

        cara = desde = hasta = 0f;
        if (areas.Count == 0) return false;

        int mejor = 0;
        float mayor = 0f;
        foreach (KeyValuePair<int, float> par in areas)
        {
            if (par.Value > mayor)
            {
                mayor = par.Value;
                mejor = par.Key;
            }
        }
        cara = mejor / 100f;
        desde = Vector3.Dot(tramo.bounds.min, largo);
        hasta = Vector3.Dot(tramo.bounds.max, largo);
        return true;
    }

    // punto pegado al panel (6 cm) y a 1.5 m del piso, sin nada entre la brocha y la pared. se busca desde el
    // centro del panel hacia las puntas (se corre si en el centro hay una puerta o ventana). primero con espacio
    // para el menu a los lados (que no se meta en la pared de la esquina) y si no cabe, con lo justo para la
    // brocha (el pasillo angosto entre casas)
    static bool FrenteALaPared(Panel panel, List<Renderer> tramos, out Vector3 punto, out Renderer objetivo)
    {
        Vector3 hacia = panel.hacia;
        Vector3 eje = Abs(hacia);
        Vector3 largo = Abs(Vector3.Cross(Vector3.up, hacia));
        float medio = (panel.desde + panel.hasta) / 2f;
        foreach (bool conMenu in new bool[] { true, false })
        {
            for (float corrido = 0f; corrido < (panel.hasta - panel.desde) / 2f - 0.2f; corrido += 0.1f)
            {
                foreach (float signo in new float[] { 1f, -1f })
                {
                    punto = largo * (medio + signo * corrido) + eje * (panel.cara + Vector3.Dot(hacia, eje) * 0.06f) + Vector3.up * (panel.alto + 1.5f);
                    // el menu mide unos 70 cm; un mueble que sobresale de la pared (la refri) tambien lo tapa
                    if (conMenu && Physics.CheckBox(punto + hacia * 0.2f, largo * 0.4f + Vector3.up * 0.35f + eje * 0.18f)) continue;
                    if (!conMenu && Physics.CheckSphere(punto + hacia * 0.12f, 0.08f)) continue;

                    RaycastHit hit;
                    if (!Physics.Raycast(punto + hacia * 0.3f, -hacia, out hit, 0.6f) || Vector3.Dot(hit.normal, hacia) < 0.9f) continue;
                    objetivo = hit.collider.GetComponent<Renderer>();
                    if (!panel.tramos.Contains(objetivo)) continue;

                    // WallSides pinta el lado que queda hacia la brocha visto desde el centro de la pared objetivo
                    Vector3 brocha = punto;
                    objetivo = panel.tramos.Find(t => Vector3.Dot(brocha - t.bounds.center, hacia) > 0f);
                    if (objetivo != null) return true;
                }
            }
        }
        punto = Vector3.zero;
        objetivo = null;
        return false;
    }

    // las plantas de la jardinera son piezas sueltas y chicas (menos de 50 cm) sobre la maceta. devuelve el
    // numero de cada triangulo de planta
    static HashSet<int> Plantas(Renderer jardinera)
    {
        Mesh malla = jardinera.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = malla.vertices;
        int[] triangulos = malla.triangles;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = jardinera.transform.TransformPoint(vertices[i]);
        int[] pieza = Piezas(vertices, triangulos);

        Dictionary<int, Bounds> cajas = new Dictionary<int, Bounds>();
        for (int i = 0; i < triangulos.Length; i++)
        {
            Bounds caja;
            if (cajas.TryGetValue(pieza[i / 3], out caja)) caja.Encapsulate(vertices[triangulos[i]]);
            else caja = new Bounds(vertices[triangulos[i]], Vector3.zero);
            cajas[pieza[i / 3]] = caja;
        }

        HashSet<int> plantas = new HashSet<int>();
        for (int t = 0; t < pieza.Length; t++)
        {
            if (Mathf.Max(cajas[pieza[t]].size.x, cajas[pieza[t]].size.z) < 0.5f)
                plantas.Add(t);
        }
        return plantas;
    }

    // los triangulos de las piezas sueltas con mucho detalle (200 triangulos o mas): en una cama son el colchon
    // y las almohadas, en una silla el asiento. lo demas (patas, bases, tablas) son cajas de pocos triangulos
    static HashSet<int> Detalladas(Renderer mueble)
    {
        Mesh malla = mueble.GetComponent<MeshFilter>().sharedMesh;
        int[] pieza = Piezas(malla.vertices, malla.triangles);
        Dictionary<int, int> cuantos = new Dictionary<int, int>();
        foreach (int raiz in pieza)
        {
            int n;
            cuantos.TryGetValue(raiz, out n);
            cuantos[raiz] = n + 1;
        }

        HashSet<int> detalladas = new HashSet<int>();
        for (int t = 0; t < pieza.Length; t++)
        {
            if (cuantos[pieza[t]] >= 200)
                detalladas.Add(t);
        }
        return detalladas;
    }

    // a que pieza suelta (triangulos conectados) pertenece cada triangulo. los vertices en el mismo punto son el
    // mismo, porque Revit los repite en cada arista
    static int[] Piezas(Vector3[] vertices, int[] triangulos)
    {
        int[] pieza = new int[vertices.Length];
        Dictionary<Vector3Int, int> enPunto = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < vertices.Length; i++)
        {
            pieza[i] = i;
            Vector3Int punto = Vector3Int.RoundToInt(vertices[i] * 1000f);
            int otro;
            if (enPunto.TryGetValue(punto, out otro)) Unir(pieza, i, otro);
            else enPunto[punto] = i;
        }
        for (int i = 0; i < triangulos.Length; i += 3)
        {
            Unir(pieza, triangulos[i], triangulos[i + 1]);
            Unir(pieza, triangulos[i], triangulos[i + 2]);
        }

        int[] deTriangulo = new int[triangulos.Length / 3];
        for (int t = 0; t < deTriangulo.Length; t++)
            deTriangulo[t] = Raiz(pieza, triangulos[t * 3]);
        return deTriangulo;
    }

    static int Raiz(int[] pieza, int i)
    {
        while (pieza[i] != i)
            i = pieza[i] = pieza[pieza[i]];
        return i;
    }

    static void Unir(int[] pieza, int a, int b)
    {
        pieza[Raiz(pieza, a)] = Raiz(pieza, b);
    }

    // debajo de las gradas no se puede parar uno: una tabla sin teletransporte en el piso, para que el rayo
    // marque ahi un destino que no sirve y no deje a uno metido en las gradas
    static void NoTeletransportarDebajo(Renderer escalera)
    {
        Bounds b = escalera.bounds;
        GameObject tabla = new GameObject("Debajo de " + escalera.name);
        tabla.transform.SetParent(escalera.transform.parent, true);
        tabla.transform.position = new Vector3(b.center.x, b.min.y + 0.02f, b.center.z);
        BoxCollider caja = tabla.AddComponent<BoxCollider>();
        caja.size = new Vector3(b.size.x / tabla.transform.lossyScale.x, 0.04f / tabla.transform.lossyScale.y, b.size.z / tabla.transform.lossyScale.z);
        tabla.transform.rotation = Quaternion.identity;
        GameObjectUtility.SetStaticEditorFlags(tabla, (StaticEditorFlags)~0);
    }

    // los postes del lote prenden de noche (FarolNocturno sigue la hora de DayNightCycle)
    static void Faroles(Transform casas)
    {
        foreach (Renderer poste in casas.GetComponentsInChildren<Renderer>())
        {
            if (!poste.name.StartsWith("Poste")) continue;

            GameObject farol = new GameObject("Farol", typeof(Light), typeof(FarolNocturno));
            farol.transform.SetParent(poste.transform, true);
            farol.transform.position = new Vector3(poste.bounds.center.x, poste.bounds.max.y - 0.4f, poste.bounds.center.z);
            Light luz = farol.GetComponent<Light>();
            luz.type = LightType.Point;
            luz.color = new Color(1f, 0.82f, 0.6f);
            luz.range = 14f;
            luz.intensity = 4f;
            luz.shadows = LightShadows.None;
            luz.enabled = false;
        }
    }

    static string Planta(Bounds pieza)
    {
        // hasta 3 m: el pedazo de pared que queda arriba de una puerta (empieza a 2.6 m) es de la planta baja
        return pieza.min.y < centroCasa.y + 3f ? "baja" : "alta";
    }

    // el punto de la losa mas cerca de su centro donde el piso queda a la vista (sin muebles ni paredes encima)
    static Vector3 PuntoLibre(Bounds losa)
    {
        Vector3 mejor = losa.center;
        float distancia = float.MaxValue;
        for (float x = losa.min.x + 0.5f; x < losa.max.x; x += 0.25f)
        {
            for (float z = losa.min.z + 0.5f; z < losa.max.z; z += 0.25f)
            {
                RaycastHit hit;
                if (!Physics.Raycast(new Vector3(x, losa.max.y + 2.2f, z), Vector3.down, out hit, 2.5f)) continue;
                if (!hit.collider.name.StartsWith("Suelo piso porcelanato")) continue;
                // el menu de piso mide casi un metro: lejos de paredes y muebles para que no se meta en ellos
                if (Physics.CheckCapsule(hit.point + Vector3.up * 0.85f, hit.point + Vector3.up * 1.9f, 0.8f)) continue;

                float d = new Vector2(x - losa.center.x, z - losa.center.z).magnitude;
                if (d < distancia)
                {
                    distancia = d;
                    mejor = hit.point;
                }
            }
        }
        return mejor;
    }

    // las 4 luces de la escena vieja (2 por planta) se pasan a la casa 1
    static void MoverLuces(Scene escena, Transform casa)
    {
        Bounds baja = new Bounds();
        Bounds alta = new Bounds();
        foreach (Renderer losa in casa.GetComponentsInChildren<Renderer>())
        {
            if (!losa.name.StartsWith("Suelo piso porcelanato")) continue;
            if (Planta(losa.bounds) == "baja")
                baja = losa.bounds;
            else
                alta = losa.bounds;
        }

        float[] lados = { -2.2f, 2.2f };
        int bajas = 0;
        int altas = 0;
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            if (raiz.name == "Luz planta baja" && bajas < 2)
                raiz.transform.position = new Vector3(baja.center.x + lados[bajas++], baja.max.y + 2.2f, baja.center.z);
            if (raiz.name == "Luz planta alta" && altas < 2)
                raiz.transform.position = new Vector3(alta.center.x + lados[altas++], alta.max.y + 2.1f, alta.center.z);
        }
    }

    // punto del inicio: frente a la casa 1, mirando hacia ella (+z, que es hacia donde deja al jugador el boton
    // "Comenzar"). el menu de bienvenida sale enfrente de la cabeza: ahi y entre el menu y la casa tiene
    // que haber espacio libre (sin carros ni gradas), para que el menu no quede metido en nada
    static Vector3 Inicio(Transform casa)
    {
        Physics.SyncTransforms();
        Bounds losa = new Bounds(centroCasa, Vector3.zero);
        foreach (Renderer pieza in casa.GetComponentsInChildren<Renderer>())
        {
            if (pieza.name.StartsWith("Suelo piso porcelanato") && Planta(pieza.bounds) == "baja")
                losa = pieza.bounds;
        }

        // lo ideal: unos 6 m frente a la casa, para verla completa al aparecer
        Vector3 ideal = new Vector3(losa.center.x, centroCasa.y, losa.min.z - 6f);
        Vector3 mejor = ideal;
        float distancia = float.MaxValue;
        for (float x = losa.min.x - 6f; x <= losa.max.x + 6f; x += 0.5f)
        {
            for (float z = losa.min.z - 14f; z <= losa.min.z - 2f; z += 0.5f)
            {
                RaycastHit hit;
                if (!Physics.Raycast(new Vector3(x, centroCasa.y + 3f, z), Vector3.down, out hit, 4f)) continue;
                if (hit.collider.GetComponent<WorldUpTeleportationArea>() == null) continue;
                if (Physics.CheckCapsule(hit.point + Vector3.up * 0.5f, hit.point + Vector3.up * 1.9f, 0.4f)) continue;

                // el menu y lo que queda detras de el (hasta la casa) libres: nada de carros ni gradas en medio
                Vector3 cabeza = hit.point + Vector3.up * 1.6f;
                if (Physics.CheckBox(cabeza + Vector3.forward * distanciaMenu, new Vector3(1.25f, 0.9f, 0.3f))) continue;
                RaycastHit frente;
                if (Physics.BoxCast(cabeza, new Vector3(2f, 0.8f, 0.1f), Vector3.forward, out frente, Quaternion.identity, 20f) && !frente.collider.transform.IsChildOf(casa)) continue;
                if (Physics.Raycast(cabeza, Vector3.forward, distanciaMenu + 1f)) continue;

                float d = Vector2.Distance(new Vector2(x, z), new Vector2(ideal.x, ideal.z));
                if (d < distancia)
                {
                    distancia = d;
                    mejor = hit.point;
                }
            }
        }

        if (distancia == float.MaxValue)
            Debug.LogWarning("No se encontro un punto libre para el inicio, se usa el frente de la casa");
        return mejor;
    }

    static Vector3 Abs(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    // ---------- terreno ----------

    // las montanas viejas no se podian caminar: el Terrain usa un terreno nuevo, plano en el valle donde esta el
    // lote y con montanas de fondo. las texturas y el zacate son los del SampleTerrain, que no se toca
    static void CrearTerreno(Scene escena, Transform casas)
    {
        Terrain terreno = null;
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            if (raiz.name == "Terrain")
                terreno = raiz.GetComponent<Terrain>();
        }
        TerrainData muestra = AssetDatabase.LoadAssetAtPath<TerrainData>(terrenoMuestra);
        if (terreno == null || muestra == null)
        {
            Debug.LogWarning("No se encontro el Terrain o el SampleTerrain, queda el terreno viejo");
            return;
        }

        // lote: todo lo que es suelo. el terreno va justo debajo del suelo mas bajo (el asfalto) para no taparlo
        Bounds lote = new Bounds(centroCasa, Vector3.zero);
        float alto = float.MaxValue;
        GameObject pino = null;
        foreach (Renderer pieza in casas.GetComponentsInChildren<Renderer>())
        {
            if (pieza.name.StartsWith("Suelo"))
            {
                lote.Encapsulate(pieza.bounds);
                alto = Mathf.Min(alto, pieza.bounds.max.y - 0.03f);
            }
            if (pieza.name.StartsWith("18039_Coniferous"))
                pino = pieza.gameObject;
        }

        string ruta = carpeta + "/Terreno valle.asset";
        AssetDatabase.DeleteAsset(ruta);
        TerrainData datos = new TerrainData();
        datos.heightmapResolution = 513;
        datos.alphamapResolution = 256;
        datos.SetDetailResolution(256, 16);
        datos.size = new Vector3(ladoTerreno, alturaMontanas + 15f, ladoTerreno);
        datos.terrainLayers = muestra.terrainLayers;
        datos.detailPrototypes = muestra.detailPrototypes;
        AssetDatabase.CreateAsset(datos, ruta);

        Vector3 origen = new Vector3(lote.center.x - ladoTerreno / 2f, alto, lote.center.z - ladoTerreno / 2f);
        terreno.transform.position = origen;
        terreno.terrainData = datos;
        terreno.detailObjectDistance = 40f;
        TerrainCollider colliderTerreno = terreno.GetComponent<TerrainCollider>();
        if (colliderTerreno != null)
            colliderTerreno.terrainData = datos;

        // alturas: 0 en el valle, despues las montanas suben con ruido
        int resolucion = datos.heightmapResolution;
        float[,] alturas = new float[resolucion, resolucion];
        for (int fila = 0; fila < resolucion; fila++)
        {
            for (int columna = 0; columna < resolucion; columna++)
            {
                float x = origen.x + columna / (float)(resolucion - 1) * ladoTerreno;
                float z = origen.z + fila / (float)(resolucion - 1) * ladoTerreno;
                float lejos = Mathf.SmoothStep(0f, 1f, (DistanciaLote(lote, x, z) - radioValle) / 70f);
                float ruido = Mathf.PerlinNoise(x * 0.012f + 10f, z * 0.012f + 10f) * 0.6f + Mathf.PerlinNoise(x * 0.04f, z * 0.04f) * 0.4f;
                alturas[fila, columna] = lejos * (0.4f + 0.6f * ruido) * alturaMontanas / datos.size.y;
            }
        }
        datos.SetHeights(0, 0, alturas);

        // texturas: zacate, manchas de tierra y roca en lo alto de las montanas
        int lado = datos.alphamapResolution;
        float[,,] mezcla = new float[lado, lado, datos.terrainLayers.Length];
        int zacate = Capa(datos, "Grass");
        int tierra = Capa(datos, "Dirt");
        int roca = Capa(datos, "Rock");
        for (int fila = 0; fila < lado; fila++)
        {
            for (int columna = 0; columna < lado; columna++)
            {
                float x = origen.x + (columna + 0.5f) / lado * ladoTerreno;
                float z = origen.z + (fila + 0.5f) / lado * ladoTerreno;
                float altura = datos.GetInterpolatedHeight((x - origen.x) / ladoTerreno, (z - origen.z) / ladoTerreno);
                float pesoRoca = roca >= 0 ? Mathf.Clamp01((altura - 15f) / 10f) : 0f;
                float pesoTierra = Mathf.Min(Mathf.Clamp01((Mathf.PerlinNoise(x * 0.05f + 3f, z * 0.05f + 7f) - 0.62f) * 3f), 1f - pesoRoca);

                if (zacate >= 0) mezcla[fila, columna, zacate] = 1f - pesoTierra - pesoRoca;
                if (tierra >= 0) mezcla[fila, columna, tierra] += pesoTierra;
                if (roca >= 0) mezcla[fila, columna, roca] += pesoRoca;
            }
        }
        datos.SetAlphamaps(0, 0, mezcla);

        // zacate alto solo en el valle (lejos no se ve y pesa), nunca dentro del lote. la ultima capa son flores
        Random.InitState(7);
        for (int capa = 0; capa < datos.detailPrototypes.Length; capa++)
        {
            int[,] detalle = new int[datos.detailHeight, datos.detailWidth];
            int maximo = capa == datos.detailPrototypes.Length - 1 ? 1 : 2;
            for (int fila = 0; fila < datos.detailHeight; fila++)
            {
                for (int columna = 0; columna < datos.detailWidth; columna++)
                {
                    float x = origen.x + (columna + 0.5f) / datos.detailWidth * ladoTerreno;
                    float z = origen.z + (fila + 0.5f) / datos.detailHeight * ladoTerreno;
                    float distancia = DistanciaLote(lote, x, z);
                    if (distancia < 1.5f || distancia > radioValle) continue;

                    if (Mathf.PerlinNoise(x * 0.1f + capa * 13f, z * 0.1f) > 0.55f)
                        detalle[fila, columna] = Random.Range(0, maximo + 1);
                }
            }
            datos.SetDetailLayer(0, 0, capa, detalle);
        }

        EditorUtility.SetDirty(datos);
        AssetDatabase.SaveAssets();

        if (terreno.GetComponent<WorldUpTeleportationArea>() == null && colliderTerreno != null)
        {
            WorldUpTeleportationArea area = terreno.gameObject.AddComponent<WorldUpTeleportationArea>();
            area.colliders.Add(colliderTerreno);
            area.interactionLayers = capaTeleport;
        }

        // copias del pino del plano, en el valle y al pie de las montanas
        if (pino == null) return;
        GameObject arboles = new GameObject("Arboles");
        int puestos = 0;
        for (int intento = 0; intento < cantidadArboles * 20 && puestos < cantidadArboles; intento++)
        {
            float x = lote.center.x + Random.Range(-radioValle - 30f, radioValle + 30f);
            float z = lote.center.z + Random.Range(-radioValle - 30f, radioValle + 30f);
            if (DistanciaLote(lote, x, z) < 8f) continue;

            GameObject copia = Object.Instantiate(pino, arboles.transform);
            copia.name = "Pino";
            copia.transform.SetPositionAndRotation(pino.transform.position, pino.transform.rotation);
            copia.transform.localScale = pino.transform.lossyScale * Random.Range(0.8f, 1.4f);

            // la malla viene con el pivote en el origen de Revit: se acomoda por la base del pino
            Bounds b = copia.GetComponent<Renderer>().bounds;
            float y = terreno.SampleHeight(new Vector3(x, 0f, z)) + origen.y;
            copia.transform.position += new Vector3(x - b.center.x, y - b.min.y, z - b.center.z);
            GameObjectUtility.SetStaticEditorFlags(copia, (StaticEditorFlags)~0);
            puestos++;
        }
    }

    // distancia en planta de un punto al borde del lote; adentro es 0
    static float DistanciaLote(Bounds lote, float x, float z)
    {
        float dx = Mathf.Max(lote.min.x - x, 0f, x - lote.max.x);
        float dz = Mathf.Max(lote.min.z - z, 0f, z - lote.max.z);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static int Capa(TerrainData datos, string nombre)
    {
        for (int i = 0; i < datos.terrainLayers.Length; i++)
        {
            if (datos.terrainLayers[i] != null && datos.terrainLayers[i].name.Contains(nombre)) return i;
        }
        return -1;
    }
}
