using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

// retoques de Apartamento2 despues de las pruebas con usuarios: sin media vuelta de golpe con el stick, manillas
// amarillas, muebles que ya no arrancan metidos en la pared, luces de noche en la casa 1, la guia de botones en la
// ayuda y las otras 3 casas con un estilo cada una (mediterraneo, industrial y minimalista: solo colores y acabados,
// la forma y el lote quedan igual). Armar los aplica al final; "Retocar" los aplica sobre la escena que ya esta armada
public static partial class ArmarPlanoNuevo
{
    // como se ve una casa estatica. la franja es una lamina de acabado (el ladrillo) en una esquina del frente
    class Estilo
    {
        public Material fachada;
        public Material adentro;
        public Material piso;
        public Material techo;
        public Material alero;
        public Material puertas;
        public Material marcos;
        public Material franja;
        // -1: esquina izquierda (la de las puertas) vista de frente, 1: esquina derecha
        public int ladoFranja;
    }

    [MenuItem("Herramientas/Retocar escena del plano nuevo")]
    static void RetocarEscena()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene escena = EditorSceneManager.OpenScene(escenaNueva, OpenSceneMode.Single);
        Transform casas = GameObject.Find("Plano nuevo").transform;
        Retocar(casas, casas.Find("Casa 1"));
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
    }

    static void Retocar(Transform casas, Transform casa1)
    {
        Physics.SyncTransforms();
        SinMediaVuelta();
        ManillasAmarillas(casa1);
        SacarMuebles(casa1);
        LucesDeNoche(casa1);
        GuiaDeBotones();

        // de la mas cerca a la casa 1 a la mas lejos
        Pintar(casas.Find("Casa 2"), Mediterraneo());
        Pintar(casas.Find("Casa 3"), Industrial());
        Pintar(casas.Find("Casa 4"), Minimalista());
        AssetDatabase.SaveAssets();
    }

    // el stick derecho hacia atras daba media vuelta de golpe y mareaba: queda solo el giro a los lados. se cambia en
    // la copia del XR Origin de esta escena, el prefab (y Apartamento.unity) quedan igual
    static void SinMediaVuelta()
    {
        foreach (ContinuousTurnProvider giro in Object.FindObjectsByType<ContinuousTurnProvider>(FindObjectsInactive.Include))
        {
            giro.enableTurnAround = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(giro);
        }
        foreach (SnapTurnProvider giro in Object.FindObjectsByType<SnapTurnProvider>(FindObjectsInactive.Include))
        {
            giro.enableTurnAround = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(giro);
        }
    }

    // la manilla amarilla y con brillo propio, para que se note de donde se agarra la puerta (tambien de noche)
    static void ManillasAmarillas(Transform casa1)
    {
        Material amarillo = Propio("Materiales", "Manilla amarilla", new Color(1f, 0.72f, 0.12f), 0.7f, 0.3f);
        amarillo.EnableKeyword("_EMISSION");
        amarillo.SetColor("_EmissionColor", new Color(0.55f, 0.36f, 0.04f));
        amarillo.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

        foreach (Renderer pieza in casa1.GetComponentsInChildren<Renderer>(true))
        {
            if (pieza.name == "Manilla")
                pieza.sharedMaterial = amarillo;
        }
    }

    // varios muebles arrancaban metidos en la pared o en otro mueble (las camas 20 cm adentro de la pared, la mesa de
    // vidrio casi medio metro): al agarrarlos la fisica los empujaba para afuera y se volvian locos. se sacan antes de
    // empezar, solo de lado (los pisos no cuentan). unas vueltas por si al sacar uno queda metido en otro
    static void SacarMuebles(Transform casa1)
    {
        int metidos = 0;
        for (int vuelta = 0; vuelta < 5; vuelta++)
        {
            metidos = 0;
            Physics.SyncTransforms();
            foreach (XRGrabInteractable mueble in casa1.GetComponentsInChildren<XRGrabInteractable>())
            {
                BoxCollider caja = mueble.GetComponent<BoxCollider>();
                foreach (Collider otro in Physics.OverlapBox(caja.bounds.center, caja.bounds.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    // las brochas flotan y se atraviesan: no cuentan
                    if (otro.transform == mueble.transform || otro.GetComponent<WorldUpTeleportationArea>() != null || otro.GetComponentInParent<BrushHover>() != null) continue;

                    Vector3 direccion;
                    float distancia;
                    if (!Physics.ComputePenetration(caja, caja.transform.position, caja.transform.rotation, otro, otro.transform.position, otro.transform.rotation, out direccion, out distancia)) continue;

                    direccion.y = 0f;
                    if (distancia < 0.005f || direccion.sqrMagnitude < 0.01f) continue;
                    mueble.transform.position += direccion.normalized * (distancia + 0.005f);
                    Physics.SyncTransforms();
                    metidos++;
                }
            }
            if (metidos == 0) break;
        }
        if (metidos > 0)
            Debug.LogWarning("Quedaron " + metidos + " muebles metidos en algo");
    }

    // de noche la casa 1 solo tenia 2 luces por planta: se busca cada metro un punto con piso de la casa abajo y sin
    // pared, se corre al centro del cuarto (cerca de una pared la luz parecia salir de ella) y ahi va un plafon en el
    // cielo con su luz, una por cuarto (si ya hay otra cerca que se ve sin pared en medio no hace falta). la luz solo
    // de noche (FarolNocturno) y sin sombras, para que el Quest aguante. sin sombras la luz atraviesa la losa: cada
    // luz alumbra solo su planta (rendering layers, ver PorPlanta)
    static void LucesDeNoche(Transform casa1)
    {
        Transform viejas = casa1.Find("Luces de noche");
        if (viejas != null)
            Object.DestroyImmediate(viejas.gameObject);

        Transform grupo = new GameObject("Luces de noche").transform;
        grupo.SetParent(casa1, false);

        Material plafon = Propio("Materiales", "Plafon", new Color(1f, 0.97f, 0.9f), 0.5f, 0f);
        plafon.EnableKeyword("_EMISSION");
        plafon.SetColor("_EmissionColor", new Color(0.7f, 0.66f, 0.58f));
        plafon.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

        foreach (Renderer losa in casa1.GetComponentsInChildren<Renderer>())
        {
            if (!losa.name.StartsWith("Suelo piso porcelanato")) continue;

            Bounds b = losa.bounds;
            uint capa = Planta(b) == "baja" ? capaBaja : capaAlta;
            for (float x = b.min.x + 0.5f; x < b.max.x; x += 1f)
            {
                for (float z = b.min.z + 0.5f; z < b.max.z; z += 1f)
                {
                    Vector3 cabeza = new Vector3(x, b.max.y + 1.4f, z);
                    RaycastHit cielo;
                    if (Physics.CheckSphere(cabeza, 0.2f) || !SobreLaLosa(cabeza, losa)) continue;
                    if (!Physics.Raycast(cabeza, Vector3.up, out cielo, 3f) || !cielo.collider.transform.IsChildOf(casa1)) continue;

                    // se centra a la altura del cielo, donde no estorban los muebles
                    Vector3 centro = AlCentro(cielo.point + Vector3.down * 0.3f);
                    cabeza = new Vector3(centro.x, cabeza.y, centro.z);
                    if (!Physics.Raycast(cabeza, Vector3.up, out cielo, 3f) || !cielo.collider.transform.IsChildOf(casa1)) continue;
                    Vector3 punto = cielo.point + Vector3.down * 0.15f;
                    bool hayOtra = false;
                    foreach (Transform otra in grupo)
                    {
                        if (Vector3.Distance(otra.position, punto) < 3.5f && !Physics.Linecast(otra.position, punto))
                            hayOtra = true;
                    }
                    if (hayOtra) continue;

                    GameObject luz = new GameObject("Luz de noche", typeof(Light), typeof(FarolNocturno));
                    luz.transform.SetParent(grupo, false);
                    luz.transform.position = punto;

                    // el plafon: un disco de 30 cm pegado al cielo, sin collider
                    GameObject disco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Object.DestroyImmediate(disco.GetComponent<Collider>());
                    disco.name = "Plafon";
                    disco.transform.position = cielo.point + Vector3.down * 0.015f;
                    disco.transform.localScale = new Vector3(0.3f, 0.01f, 0.3f);
                    disco.transform.SetParent(luz.transform, true);
                    disco.GetComponent<MeshRenderer>().sharedMaterial = plafon;
                    GameObjectUtility.SetStaticEditorFlags(disco, (StaticEditorFlags)~0);

                    Light foco = luz.GetComponent<Light>();
                    foco.type = LightType.Point;
                    foco.color = new Color(1f, 0.86f, 0.68f);
                    foco.range = 7f;
                    foco.intensity = 1.8f;
                    foco.GetUniversalAdditionalLightData().renderingLayers = capa;
                    foco.shadows = LightShadows.None;
                    foco.enabled = false;
                }
            }
        }
        PorPlanta(casa1);
        LuzDeMenus();

        // las 4 luces de siempre (MoverLuces) tambien atravesaban la losa
        foreach (GameObject raiz in casa1.gameObject.scene.GetRootGameObjects())
        {
            if (raiz.name == "Luz planta baja" || raiz.name == "Luz planta alta")
                raiz.GetComponent<Light>().GetUniversalAdditionalLightData().renderingLayers = raiz.name == "Luz planta baja" ? capaBaja : capaAlta;
        }
        Debug.Log("Luces de noche en la casa 1: " + grupo.childCount);
    }

    // la luz de noche de los menus de acabados (ver MenuLibre): direccional, sin sombras, solo en la capa 3
    static void LuzDeMenus()
    {
        foreach (MenuLibre menu in Object.FindObjectsByType<MenuLibre>(FindObjectsInactive.Include))
        {
            if (menu.menuLight == null)
            {
                GameObject luz = new GameObject("Luz de menus", typeof(Light), typeof(FarolNocturno));
                luz.transform.SetParent(menu.transform, false);
                menu.menuLight = luz.GetComponent<Light>();
            }
            Light foco = menu.menuLight;
            foco.type = LightType.Directional;
            foco.color = Color.white;
            foco.intensity = 1.2f;
            foco.shadows = LightShadows.None;
            foco.enabled = false;
            foco.GetUniversalAdditionalLightData().renderingLayers = 1u << 3;
        }
    }

    // capas de luz de cada planta (la 0, Default, la siguen usando el sol, los faroles y las luces de siempre)
    const uint capaBaja = 1u << 1;
    const uint capaAlta = 1u << 2;

    // cada pieza de la casa 1 queda en la capa de su planta; la losa de arriba y lo que cruza las dos (gradas), en las dos
    static void PorPlanta(Transform casa1)
    {
        Bounds losaAlta = new Bounds();
        foreach (Renderer losa in casa1.GetComponentsInChildren<Renderer>())
        {
            if (losa.name.StartsWith("Suelo piso porcelanato") && Planta(losa.bounds) == "alta")
                losaAlta = losa.bounds;
        }

        foreach (Renderer pieza in casa1.GetComponentsInChildren<Renderer>(true))
        {
            bool baja = pieza.bounds.min.y < losaAlta.min.y - 0.3f;
            bool alta = pieza.bounds.max.y > losaAlta.max.y + 0.3f;
            uint capas = 1u;
            if (baja || !alta) capas |= capaBaja;
            if (alta || !baja) capas |= capaAlta;
            pieza.renderingLayerMask = capas;
        }
    }

    // el punto a medio camino entre las paredes de enfrente, en x y en z (si en ese eje no hay pared cerca queda igual)
    static Vector3 AlCentro(Vector3 punto)
    {
        Vector3[] ejes = { Vector3.right, Vector3.forward };
        foreach (Vector3 eje in ejes)
        {
            RaycastHit uno;
            RaycastHit otro;
            if (Physics.Raycast(punto, eje, out uno, 6f) && Physics.Raycast(punto, -eje, out otro, 6f))
                punto += eje * (Vector3.Dot(uno.point - punto, eje) - Vector3.Dot(punto - otro.point, eje)) / 2f;
        }
        return punto;
    }

    // abajo del punto esta esa losa (puede haber una mesa en medio)
    static bool SobreLaLosa(Vector3 punto, Renderer losa)
    {
        foreach (RaycastHit hit in Physics.RaycastAll(punto, Vector3.down, 2f))
        {
            if (hit.collider.gameObject == losa.gameObject) return true;
        }
        return false;
    }

    // la guia de botones del Quest va junto a la ayuda, en el mismo objeto que WelcomeMenu
    static void GuiaDeBotones()
    {
        Sprite circulo = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/circulo-suave.png");
        foreach (WelcomeMenu menu in Object.FindObjectsByType<WelcomeMenu>(FindObjectsInactive.Include))
        {
            GuiaControles guia = menu.GetComponent<GuiaControles>();
            if (guia == null)
                guia = menu.gameObject.AddComponent<GuiaControles>();
            guia.circle = circulo;
        }
    }

    // ---------- estilos de las casas estaticas ----------

    static Estilo Mediterraneo()
    {
        Estilo estilo = new Estilo();
        estilo.fachada = Tenido("Estuco crema", DeLaEscena("Repello marfil"), new Color(0.98f, 0.84f, 0.5f));
        estilo.adentro = Tenido("Crema claro", DeLaEscena("Repello marfil"), new Color(0.98f, 0.93f, 0.82f));
        estilo.piso = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materiales/Pisos/Roble claro.mat");
        estilo.techo = Tenido("Teja de barro", AssetDatabase.LoadAssetAtPath<Material>("Assets/Materiales/Pisos/Terracota.mat"), Color.white);
        estilo.alero = estilo.techo;
        estilo.puertas = AssetDatabase.LoadAssetAtPath<Material>(carpeta + "/Acabados/Nogal.mat");
        estilo.marcos = estilo.puertas;
        return estilo;
    }

    static Estilo Industrial()
    {
        Estilo estilo = new Estilo();
        estilo.fachada = Tenido("Concreto aparente", AssetDatabase.LoadAssetAtPath<Material>("Assets/YughuesFreeConcreteMaterials/Materials/M_YFCM_BrushedWorn.mat"), new Color(0.7f, 0.69f, 0.67f));
        estilo.adentro = Tenido("Concreto gris", AssetDatabase.LoadAssetAtPath<Material>("Assets/YughuesFreeConcreteMaterials/Materials/M_YFCM_Plastered.mat"), new Color(0.7f, 0.7f, 0.7f));
        estilo.piso = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materiales/Pisos/Nogal oscuro.mat");
        estilo.techo = Tenido("Techo negro", DeLaEscena("Piso gris claro"), new Color(0.1f, 0.1f, 0.11f));
        estilo.alero = Tenido("Alero oscuro", DeLaEscena("Piso gris claro"), new Color(0.14f, 0.14f, 0.15f));
        estilo.puertas = AssetDatabase.LoadAssetAtPath<Material>(carpeta + "/Acabados/Negro satinado.mat");
        estilo.marcos = estilo.puertas;
        estilo.franja = Tenido("Ladrillo rojo", DeLaEscena("Piso gris claro"), Color.white);
        estilo.franja.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(carpeta + "/Materiales/Ladrillo rojo.png");
        EditorUtility.SetDirty(estilo.franja);
        estilo.ladoFranja = 1;
        return estilo;
    }

    static Estilo Minimalista()
    {
        Estilo estilo = new Estilo();
        estilo.fachada = Tenido("Blanco liso", DeLaEscena("Piso gris claro"), new Color(0.97f, 0.97f, 0.96f));
        estilo.adentro = Tenido("Blanco interior", DeLaEscena("Piso gris claro"), new Color(0.93f, 0.93f, 0.92f));
        estilo.piso = Tenido("Marmol gris claro", AssetDatabase.LoadAssetAtPath<Material>("Assets/Materiales/Pisos/Marmol blanco.mat"), new Color(0.82f, 0.82f, 0.82f));
        estilo.techo = Tenido("Techo gris oscuro", DeLaEscena("Piso gris claro"), new Color(0.24f, 0.25f, 0.27f));
        estilo.alero = Tenido("Alero gris oscuro", DeLaEscena("Piso gris claro"), new Color(0.3f, 0.31f, 0.33f));
        estilo.puertas = AssetDatabase.LoadAssetAtPath<Material>(carpeta + "/Acabados/Negro satinado.mat");
        estilo.marcos = estilo.puertas;
        return estilo;
    }

    // copia de un material del proyecto con otro color, en Plano nuevo/Estilos
    static Material Tenido(string nombre, Material fuente, Color color)
    {
        if (!AssetDatabase.IsValidFolder(carpeta + "/Estilos"))
            AssetDatabase.CreateFolder(carpeta, "Estilos");

        string ruta = carpeta + "/Estilos/" + nombre + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            material = new Material(fuente);
            AssetDatabase.CreateAsset(material, ruta);
        }
        else
        {
            material.CopyPropertiesFromMaterial(fuente);
        }
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    // pinta una casa estatica: las caras que dan afuera con la fachada y las de adentro con el interior. la losa
    // lleva el piso arriba y el techo su color arriba y el del alero en las orillas y por debajo (abajo del alero estan
    // las gradas, que son de la casa: con Afuera quedaba del color de adentro)
    static void Pintar(Transform casa, Estilo estilo)
    {
        string subcarpeta = "Estilo " + casa.name;
        foreach (Renderer pieza in casa.GetComponentsInChildren<Renderer>())
        {
            string nombre = pieza.name;
            if (nombre.StartsWith("Muro"))
                PorCaras(casa, pieza, (centro, n) => Afuera(casa, centro, n) ? 0 : 1, new Material[] { estilo.fachada, estilo.adentro }, subcarpeta);
            else if (nombre.StartsWith("Suelo piso porcelanato"))
                PorCaras(casa, pieza, (centro, n) => n.y > 0.7f ? 0 : Afuera(casa, centro, n) ? 1 : 2, new Material[] { estilo.piso, estilo.fachada, estilo.adentro }, subcarpeta);
            else if (nombre.StartsWith("Cubierta"))
                PorCaras(casa, pieza, (centro, n) => n.y > 0.3f ? 0 : 1, new Material[] { estilo.techo, estilo.alero }, subcarpeta);
            else if (EsPuerta(nombre))
                pieza.sharedMaterial = estilo.puertas;
            else if (nombre.StartsWith("Windows_") || nombre.StartsWith("Aluprof_MB-45"))
            {
                Bounds ventana = pieza.bounds;
                PorCaras(casa, pieza, (centro, n) => EnElMarco(ventana, centro) ? 0 : 1, new Material[] { estilo.marcos, DeLaEscena("Vidrio celeste") }, subcarpeta);
            }
        }

        Transform vieja = casa.Find("Franja");
        if (vieja != null)
            Object.DestroyImmediate(vieja.gameObject);
        if (estilo.franja != null)
            Franja(casa, estilo.franja, estilo.ladoFranja, subcarpeta);
    }

    // PorMateriales con el slot de cada cara. si todas las caras caen en un solo slot la pieza queda entera con ese
    // material. el collider sigue a la malla nueva (PorMateriales mueve la pieza a su centro)
    static void PorCaras(Transform casa, Renderer pieza, System.Func<Vector3, Vector3, int> slot, Material[] materiales, string subcarpeta)
    {
        // ya pintada con los mismos slots (se corrio Retocar otra vez): solo se ponen los materiales
        MeshFilter filtro = pieza.GetComponent<MeshFilter>();
        if (AssetDatabase.GetAssetPath(filtro.sharedMesh).Contains("/" + subcarpeta + "/") && filtro.sharedMesh.subMeshCount == materiales.Length)
        {
            System.Array.Resize(ref materiales, filtro.sharedMesh.subMeshCount);
            pieza.sharedMaterials = materiales;
            return;
        }

        int[] porSlot = new int[materiales.Length];
        PorMateriales(pieza, (i, centro, n) =>
        {
            int s = slot(centro, n);
            porSlot[s]++;
            return s;
        }, materiales, subcarpeta);

        if (pieza.sharedMaterials.Length == 1)
        {
            int mayor = 0;
            for (int s = 1; s < porSlot.Length; s++)
            {
                if (porSlot[s] > porSlot[mayor]) mayor = s;
            }
            pieza.sharedMaterial = materiales[mayor];
            return;
        }

        // si el ultimo slot quedo vacio, CombineMeshes no le hace submalla: un material de mas volveria a dibujar la
        // ultima submalla encima
        System.Array.Resize(ref materiales, filtro.sharedMesh.subMeshCount);
        pieza.sharedMaterials = materiales;

        MeshCollider collider = pieza.GetComponent<MeshCollider>();
        if (collider != null)
            collider.sharedMesh = filtro.sharedMesh;
        Physics.SyncTransforms();
    }

    // la ventana es un vidrio con su marco alrededor: lo que queda a menos de 7 cm de la orilla es marco
    static bool EnElMarco(Bounds ventana, Vector3 centro)
    {
        float lado = ventana.size.x > ventana.size.z ? centro.x : centro.z;
        float min = ventana.size.x > ventana.size.z ? ventana.min.x : ventana.min.z;
        float max = ventana.size.x > ventana.size.z ? ventana.max.x : ventana.max.z;
        float orilla = Mathf.Min(Mathf.Min(lado - min, max - lado), Mathf.Min(centro.y - ventana.min.y, ventana.max.y - centro.y));
        return orilla < 0.07f;
    }

    // una cara da afuera si hacia donde mira no hay nada de su misma casa (la casa de al lado no cuenta)
    static bool Afuera(Transform casa, Vector3 centro, Vector3 n)
    {
        foreach (RaycastHit hit in Physics.RaycastAll(centro + n * 0.02f, n, 40f))
        {
            if (hit.collider.transform.IsChildOf(casa)) return false;
        }
        return true;
    }

    // lamina de acabado en una esquina del frente (la fachada mira hacia -z), tan ancha como deje la ventana o puerta
    // mas cerca de esa esquina
    static void Franja(Transform casa, Material material, int lado, string subcarpeta)
    {
        Bounds muros = new Bounds();
        bool primero = true;
        foreach (Renderer pieza in casa.GetComponentsInChildren<Renderer>())
        {
            if (!pieza.name.StartsWith("Muro")) continue;
            if (primero) muros = pieza.bounds;
            else muros.Encapsulate(pieza.bounds);
            primero = false;
        }

        // si de ese lado no cabe se prueba la otra esquina
        foreach (int esquina in new int[] { lado, -lado })
        {
            float borde = esquina > 0 ? muros.max.x : muros.min.x;
            RaycastHit hit;
            if (!Physics.Raycast(new Vector3(borde - esquina * 0.1f, muros.min.y + 1.5f, muros.min.z - 3f), Vector3.forward, out hit, 6f) || !hit.collider.name.StartsWith("Muro")) continue;
            float frente = hit.point.z;

            // de abajo hasta donde empieza el techo (las paredes siguen un poco por detras del alero)
            float arriba = muros.max.y;
            foreach (Renderer techo in casa.GetComponentsInChildren<Renderer>())
            {
                if (techo.name.StartsWith("Cubierta"))
                    arriba = Mathf.Min(arriba, techo.bounds.min.y);
            }

            // espacio libre desde la esquina hasta la ventana o puerta del frente mas cerca (de la altura de la franja)
            float libre = 0.9f;
            foreach (Renderer hueco in casa.GetComponentsInChildren<Renderer>())
            {
                bool esHueco = hueco.name.StartsWith("Windows_") || hueco.name.StartsWith("Aluprof") || EsPuerta(hueco.name);
                if (!esHueco || hueco.bounds.min.z > frente + 0.5f || hueco.bounds.min.y > arriba) continue;
                libre = Mathf.Min(libre, esquina > 0 ? borde - hueco.bounds.max.x : hueco.bounds.min.x - borde);
            }
            float ancho = libre - 0.05f;
            if (ancho < 0.2f) continue;

            Vector3 tamano = new Vector3(ancho, arriba - muros.min.y, 0.03f);
            Vector3 centro = new Vector3(borde - esquina * ancho / 2f, (arriba + muros.min.y) / 2f, frente - 0.015f);
            GameObject primitivo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh malla = Object.Instantiate(primitivo.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(primitivo);

            Vector3[] vertices = malla.vertices;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = Vector3.Scale(vertices[i], tamano);
            malla.vertices = vertices;
            malla.RecalculateBounds();
            malla.uv = UVsDeMundo(malla, centro);
            Guardar(malla, subcarpeta, "Franja");

            GameObject franja = new GameObject("Franja", typeof(MeshFilter), typeof(MeshRenderer));
            franja.transform.position = centro;
            franja.transform.SetParent(casa, true);
            franja.GetComponent<MeshFilter>().sharedMesh = malla;
            franja.GetComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(franja, (StaticEditorFlags)~0);
            return;
        }
        Debug.LogWarning("No cupo la franja de acabado en " + casa.name);
    }
}
