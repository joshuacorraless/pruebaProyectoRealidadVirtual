<div align="center">

<h1>Proyecto Realidad Virtual</h1>

<p><b>Recorrido en VR de un modelado de apartamentos de dos pisos</b></p>

<p>
  <img src="https://img.shields.io/badge/Unity-6000.5.8f1-222222?style=for-the-badge&logo=unity&logoColor=white" alt="Unity 6">
  <img src="https://img.shields.io/badge/Meta%20Quest-Android%20APK-0467DF?style=for-the-badge&logo=meta&logoColor=white" alt="Meta Quest">
  <img src="https://img.shields.io/badge/XR%20Toolkit-3.5.1-7B3FE4?style=for-the-badge" alt="XR Interaction Toolkit">
  <img src="https://img.shields.io/badge/OpenXR-1.17-E4572E?style=for-the-badge" alt="OpenXR">
  <img src="https://img.shields.io/badge/URP-17.3-2E9E6B?style=for-the-badge" alt="URP">
</p>

</div>

<br>

## Funcionalidades

<table>
  <tr>
    <td width="50%" valign="top">
      <img src="https://img.shields.io/badge/Recorrido-0467DF?style=flat-square" alt="Recorrido"><br>
      Caminar y teletransportarse por las dos plantas de la casa.
    </td>
    <td width="50%" valign="top">
      <img src="https://img.shields.io/badge/Puertas-E4A11B?style=flat-square" alt="Puertas"><br>
      Abrir y cerrar puertas agarrando la manilla amarilla.
    </td>
  </tr>
  <tr>
    <td valign="top">
      <img src="https://img.shields.io/badge/Muebles-8E5B3A?style=flat-square" alt="Muebles"><br>
      Mover muebles y cambiarles el acabado.
    </td>
    <td valign="top">
      <img src="https://img.shields.io/badge/Acabados-7B3FE4?style=flat-square" alt="Acabados"><br>
      Cambiar el acabado de paredes y pisos de cada cuarto con las brochas.
    </td>
  </tr>
  <tr>
    <td valign="top">
      <img src="https://img.shields.io/badge/D%C3%ADa%20y%20noche-1F2A44?style=flat-square" alt="Dia y noche"><br>
      Cambiar la hora del dia; los faroles de la calle se encienden de noche.
    </td>
    <td valign="top">
      <img src="https://img.shields.io/badge/Exterior-2E9E6B?style=flat-square" alt="Exterior"><br>
      Calle, carros, casas vecinas y valle alrededor del lote.
    </td>
  </tr>
</table>

## Contenido entregado

| Archivo / carpeta | Descripcion |
|:--|:--|
| `ProyectoRealidadVirtual.apk` | Aplicacion lista para instalar en el Meta Quest (Android). |
| `pruebaProyectoRealidadVirtual/` | Proyecto completo de Unity (codigo fuente). |
| `README.md` | Este archivo. |
| `ASSETS_EXTERNOS.md` | Lista de assets externos: nombre, fuente y licencia. |
| `REPORTE_IA.md` | Reporte de uso de herramientas de IA. |

<details>
<summary><b>Estructura del proyecto de Unity</b></summary>
<br>

| Carpeta | Contenido |
|:--|:--|
| `Assets/Scenes/Apartamento.unity` | Escena principal: la casa original. |
| `Assets/Scenes/Apartamento2.unity` | Escena con el plano nuevo: casa mas grande, carros, calle, faroles y valle. |
| `Assets/Scripts/` | Scripts propios: menus, puertas, cambio de materiales, pintura de paredes y pisos, ciclo dia/noche y faroles. |
| `Assets/Editor/` | Herramienta que arma la escena Apartamento2 a partir del modelo (*Herramientas > Armar escena del plano nuevo*). |
| `Assets/Modelos/` | Modelos de las casas exportados de Blender (`.fbx`) con sus materiales y acabados. |
| `Assets/Materiales/` · `Assets/Sprites/` · `Assets/Prefabs/` | Materiales, iconos y prefabs propios. |
| `Modelado/Apartamento.blend` | Archivo original de Blender de la primera casa. |
| `ADG_Textures` · `Fantasy Skybox FREE` · `Sinister_Heart` · `YughuesFreeConcreteMaterials` · `VRTemplateAssets` · `Samples/` | Assets externos (ver `ASSETS_EXTERNOS.md`). |

</details>

## Instalar el APK en el Meta Quest

<img src="https://img.shields.io/badge/Paso%201-Modo%20desarrollador-0467DF?style=flat-square" alt="Paso 1">

En el celular, app *Meta Horizon* > Dispositivos > el visor > **Modo desarrollador**.

<img src="https://img.shields.io/badge/Paso%202-Conectar%20por%20USB--C-0467DF?style=flat-square" alt="Paso 2">

Conectar el Quest a la computadora y, dentro del visor, aceptar **Permitir depuracion USB**.

<img src="https://img.shields.io/badge/Paso%203-Instalar%20el%20APK-0467DF?style=flat-square" alt="Paso 3">

Con cualquiera de estas opciones:

- **SideQuest:** arrastrar el `.apk` a la ventana.
- **Meta Quest Developer Hub:** *Device Manager > Apps > Add Build*.
- **ADB:**

```bash
adb install -r ProyectoRealidadVirtual.apk
```

<img src="https://img.shields.io/badge/Paso%204-Abrir%20la%20app-0467DF?style=flat-square" alt="Paso 4">

En el visor: *Biblioteca de apps* > filtro **Origenes desconocidos** > **Proyecto Realidad Virtual**.

> [!NOTE]
> Paquete `com.joshuacorraless.proyectorealidadvirtual` · requiere Android 14 (API 34) o superior.

## Abrir el proyecto en Unity

1. Instalar **Unity 6000.5.8f1** desde Unity Hub con el modulo **Android Build Support** (incluye OpenJDK y Android SDK/NDK).
2. En Unity Hub: *Add > Add project from disk* y elegir la carpeta `pruebaProyectoRealidadVirtual`.
3. Abrir `Assets/Scenes/Apartamento.unity` o `Assets/Scenes/Apartamento2.unity`.
4. **Sin visor:** darle *Play*; el XR Device Simulator permite moverse con teclado y mouse.
5. **Con el Quest por cable:** *File > Build Profiles > Android*, elegir el Quest en *Run Device* y darle *Build and Run*.

> [!TIP]
> La primera vez que se abre, Unity descarga los paquetes e importa los assets; puede tardar varios minutos.

## Controles

| Control | Accion |
|:--|:--|
| <img src="https://img.shields.io/badge/Stick%20izquierdo-0467DF?style=flat-square" alt="Stick izquierdo"> | Caminar |
| <img src="https://img.shields.io/badge/Stick%20derecho-0467DF?style=flat-square" alt="Stick derecho"> | A los lados gira · hacia arriba y apuntando al piso, teletransporte |
| <img src="https://img.shields.io/badge/Grip-E4A11B?style=flat-square" alt="Grip"> | Agarrar puertas (de la manilla amarilla) y muebles |
| <img src="https://img.shields.io/badge/Gatillo-E4572E?style=flat-square" alt="Gatillo"> | Escoger brochas, muestras de acabado y botones |
| <kbd>A</kbd> &nbsp;mando derecho | Menu de la hora del dia |
| <kbd>B</kbd> &nbsp;mando derecho | Abrir o cerrar la ayuda |

Las brochas que flotan junto a paredes y pisos cambian el acabado de ese cuarto.
