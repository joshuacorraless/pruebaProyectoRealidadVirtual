using System.Collections.Generic;
using UnityEngine;

// MaterialChanger pone el menu de acabados de los muebles enfrente del jugador y solo revisa que no haya nada
// justo al frente, asi que sus costados se podian meter en una pared o en un mueble. este script (uno en la escena)
// acerca ese menu al jugador, y lo achica igual para que se vea del mismo tamano, hasta que todo el menu quede libre.
// los menus de las brochas de pared y piso ya salen en un lugar revisado y no se tocan
public class MenuLibre : MonoBehaviour
{
    // medio ancho del menu a la distancia normal de MaterialChanger (1.5 m)
    public float halfSize = 0.33f;

    // de noche las muestras de los menus se veian oscuras: esta luz (solo de noche, con FarolNocturno) alumbra solo
    // la capa de los menus (rendering layer 3) y sigue a la cabeza, asi alumbra lo que el jugador ve como de dia
    public Light menuLight;

    RadialMenu checkedMenu;
    List<Renderer> parts = new List<Renderer>();

    void LateUpdate()
    {
        RadialMenu menu = FindFirstObjectByType<RadialMenu>();
        if (menu == null) return;

        // cada frame: al pasar de pagina el menu crea botones nuevos
        menu.GetComponentsInChildren(true, parts);
        foreach (Renderer part in parts)
            part.renderingLayerMask |= 1u << 3;
        if (menuLight != null && Camera.main != null)
            menuLight.transform.rotation = Camera.main.transform.rotation;

        if (menu == checkedMenu || Camera.main == null) return;
        checkedMenu = menu;

        foreach (MaterialChanger brush in FindObjectsByType<MaterialChanger>(FindObjectsSortMode.None))
        {
            if ((brush.menuOnWall || brush.menuOnFloor) && Vector3.Distance(brush.transform.position, menu.transform.position) < 0.2f)
                return;
        }

        Transform head = Camera.main.transform;
        Vector3 toMenu = menu.transform.position - head.position;
        float distance = toMenu.magnitude;
        float start = distance;
        while (distance > 0.35f && Blocked(head.position + toMenu.normalized * distance, menu.transform.rotation, halfSize * distance / 1.5f, head))
            distance -= 0.05f;

        menu.transform.position = head.position + toMenu.normalized * distance;
        menu.transform.localScale *= distance / start;
    }

    // si el menu (un cuadro de ese tamano) se mete en algo que no sea el jugador
    static bool Blocked(Vector3 position, Quaternion rotation, float half, Transform head)
    {
        foreach (Collider other in Physics.OverlapBox(position, new Vector3(half, half, 0.03f), rotation,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (!other.transform.IsChildOf(head.root))
                return true;
        }
        return false;
    }
}
