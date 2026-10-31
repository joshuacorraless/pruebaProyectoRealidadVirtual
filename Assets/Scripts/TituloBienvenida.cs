using System.Collections;
using TMPro;
using UnityEngine;

// cambia el titulo del cartel de bienvenida (WelcomeMenu lo arma al empezar con el titulo del avance)
[RequireComponent(typeof(WelcomeMenu))]
public class TituloBienvenida : MonoBehaviour
{
    public string title = "Proyecto Realidad Virtual";

    IEnumerator Start()
    {
        // se espera a que WelcomeMenu arme el cartel (lo hace cuando ya hay camara)
        while (true)
        {
            foreach (TextMeshProUGUI label in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (label.text.StartsWith("Proyecto Realidad Virtual"))
                {
                    label.text = title;
                    yield break;
                }
            }
            yield return null;
        }
    }
}
