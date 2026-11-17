<div align="center">

<h1>🏠 Proyecto Realidad Virtual</h1>

<p><b>Recorrido inmersivo en VR de una casa de dos plantas para Meta Quest</b></p>

<p>
  <img src="https://img.shields.io/badge/Unity-6000.5.8f1-000000?style=for-the-badge&logo=unity&logoColor=white" alt="Unity 6">
  <img src="https://img.shields.io/badge/Meta%20Quest-VR-0467DF?style=for-the-badge&logo=meta&logoColor=white" alt="Meta Quest">
  <img src="https://img.shields.io/badge/OpenXR-XR%20Toolkit%203.5.1-5C2D91?style=for-the-badge" alt="OpenXR">
  <img src="https://img.shields.io/badge/URP-17.3-2E7D32?style=for-the-badge" alt="URP">
</p>

<p><i>TEC · Realidad Virtual · Semestre 6</i></p>

</div>

<hr>

<h2>✨ Que se puede hacer</h2>

<table>
  <tr>
    <td align="center" width="25%">🚶<br><b>Caminar y teletransportarse</b><br><sub>por las dos plantas de la casa</sub></td>
    <td align="center" width="25%">🚪<br><b>Abrir puertas</b><br><sub>agarrando la manilla amarilla</sub></td>
    <td align="center" width="25%">🛋️<br><b>Mover muebles</b><br><sub>y cambiarles el acabado</sub></td>
    <td align="center" width="25%">🖌️<br><b>Cambiar acabados</b><br><sub>de paredes y pisos por cuarto</sub></td>
  </tr>
  <tr>
    <td align="center">🌗<br><b>Dia y noche</b><br><sub>con menu de la hora del dia</sub></td>
    <td align="center">💡<br><b>Faroles</b><br><sub>que se encienden de noche</sub></td>
    <td align="center">🚗<br><b>Entorno exterior</b><br><sub>calle, carros y valle</sub></td>
    <td align="center">❓<br><b>Guia de controles</b><br><sub>dentro del juego</sub></td>
  </tr>
</table>

<h2>📦 Contenido entregado</h2>

<table>
  <tr><th>Archivo / carpeta</th><th>Que es</th></tr>
  <tr><td><code>ProyectoRealidadVirtual.apk</code></td><td>Aplicacion lista para instalar en el Meta Quest (Android).</td></tr>
  <tr><td><code>pruebaProyectoRealidadVirtual/</code></td><td>Proyecto completo de Unity (codigo fuente).</td></tr>
  <tr><td><code>README.md</code></td><td>Este archivo.</td></tr>
  <tr><td><code>ASSETS_EXTERNOS.md</code></td><td>Lista de assets externos: nombre, fuente y licencia.</td></tr>
  <tr><td><code>REPORTE_IA.md</code></td><td>Reporte de uso de herramientas de IA.</td></tr>
</table>

<details>
<summary><b>📁 Estructura del proyecto de Unity</b></summary>
<br>

<table>
  <tr><th>Carpeta</th><th>Que contiene</th></tr>
  <tr><td><code>Assets/Scenes/Apartamento.unity</code></td><td>Escena principal: la casa original.</td></tr>
  <tr><td><code>Assets/Scenes/Apartamento2.unity</code></td><td>Escena con el plano nuevo: casa mas grande, carros, calle, faroles y valle.</td></tr>
  <tr><td><code>Assets/Scripts/</code></td><td>Scripts propios: menus (radial, bienvenida, controles), puertas, cambio de materiales, pintura de paredes y pisos, ciclo dia/noche y faroles.</td></tr>
  <tr><td><code>Assets/Editor/</code></td><td>Herramienta de editor que arma la escena Apartamento2 a partir del modelo (<i>Herramientas &gt; Armar escena del plano nuevo</i>).</td></tr>
  <tr><td><code>Assets/Modelos/</code></td><td>Modelos de las casas exportados de Blender (<code>.fbx</code>) con sus materiales y acabados.</td></tr>
  <tr><td><code>Assets/Materiales/</code>, <code>Assets/Sprites/</code>, <code>Assets/Prefabs/</code></td><td>Materiales, iconos y prefabs propios.</td></tr>
  <tr><td><code>Modelado/Apartamento.blend</code></td><td>Archivo original de Blender de la primera casa.</td></tr>
  <tr><td><code>ADG_Textures</code>, <code>Fantasy Skybox FREE</code>, <code>Sinister_Heart</code>, <code>YughuesFreeConcreteMaterials</code>, <code>VRTemplateAssets</code>, <code>Samples/</code></td><td>Assets externos (ver <code>ASSETS_EXTERNOS.md</code>).</td></tr>
</table>

</details>

<h2>🥽 Instalar el APK en el Meta Quest</h2>

<ol>
  <li>Activar el <b>modo desarrollador</b> del Quest: app <i>Meta Horizon</i> en el celular &gt; Dispositivos &gt; el visor &gt; Modo desarrollador.</li>
  <li>Conectar el Quest a la computadora con cable USB-C y, dentro del visor, aceptar <b>"Permitir depuracion USB"</b>.</li>
  <li>Instalar el APK con una de estas opciones:
    <ul>
      <li><b>SideQuest</b>: arrastrar el <code>.apk</code> a la ventana de SideQuest.</li>
      <li><b>Meta Quest Developer Hub</b>: Device Manager &gt; Apps &gt; Add Build y elegir el <code>.apk</code>.</li>
      <li><b>ADB</b> (linea de comandos):</li>
    </ul>
  </li>
</ol>

```bash
adb install -r ProyectoRealidadVirtual.apk
```

<ol start="4">
  <li>En el visor: Biblioteca de apps &gt; filtro <b>"Origenes desconocidos"</b> &gt; <b>Proyecto Realidad Virtual</b>.</li>
</ol>

<blockquote>
  <b>Paquete:</b> <code>com.joshuacorraless.proyectorealidadvirtual</code> · Android 14 (API 34) como minimo.
</blockquote>

<h2>🛠️ Abrir y ejecutar el proyecto en Unity</h2>

<ol>
  <li>Instalar <b>Unity 6000.5.8f1</b> desde Unity Hub con el modulo <b>Android Build Support</b> (incluye OpenJDK y Android SDK/NDK).</li>
  <li>En Unity Hub: <i>Add &gt; Add project from disk</i> y elegir la carpeta <code>pruebaProyectoRealidadVirtual</code>. La primera vez Unity descarga los paquetes y tarda unos minutos.</li>
  <li>Abrir la escena <code>Assets/Scenes/Apartamento.unity</code> (o <code>Apartamento2.unity</code>).</li>
  <li><b>Sin visor:</b> darle <b>Play</b>; el XR Device Simulator permite moverse con teclado y mouse.</li>
  <li><b>Con el Quest por cable:</b> <i>File &gt; Build Profiles &gt; Android</i>, elegir el Quest en <i>Run Device</i> y darle <b>Build and Run</b>.</li>
</ol>

<h2>🎮 Controles en el visor</h2>

<table>
  <tr><th>Boton</th><th>Accion</th></tr>
  <tr><td>🕹️ <b>Stick izquierdo</b></td><td>Caminar</td></tr>
  <tr><td>🕹️ <b>Stick derecho</b></td><td>A los lados gira · hacia arriba y apuntando al piso, teletransporte</td></tr>
  <tr><td>✊ <b>Grip</b> (costado)</td><td>Agarrar puertas (de la manilla amarilla) y muebles</td></tr>
  <tr><td>👆 <b>Gatillo</b></td><td>Escoger brochas, muestras de acabado y botones</td></tr>
  <tr><td>🅰️ <b>A</b> (mando derecho)</td><td>Menu de la hora del dia</td></tr>
  <tr><td>🅱️ <b>B</b> (mando derecho)</td><td>Abrir o cerrar la ayuda</td></tr>
</table>

<p>Las <b>brochas</b> que flotan junto a paredes y pisos cambian el acabado de ese cuarto.</p>

<hr>

<div align="center">
  <sub>Hecho por el equipo del curso de Realidad Virtual · TEC</sub>
</div>
