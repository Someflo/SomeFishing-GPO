# SomeFishing GPO

Macro visual para el minijuego de pesca de GPO en Windows. Su objetivo es ofrecer un programa sencillo, transparente y revisable, con el código completo y una forma de compilarlo localmente.

**Versión 0.2.0: lectura de cebo y saltos durante la espera.** Esta compilación se entrega para pruebas locales; su publicación en GitHub está pendiente. Todavía necesita validación en partidas reales. No promete una tasa de capturas, evitar todas las desconexiones ni una garantía absoluta de ausencia de virus.

![Vista del detector de SomeFishing GPO; imagen sintética](docs/Vista-previa.png)

## Cómo funciona

El programa localiza la barra azul dentro de una zona seleccionada y mantiene la línea blanca del pez dentro del hueco gris: mantener el clic sube el hueco; soltarlo lo baja. La barra verde indica el progreso del juego y se excluye del seguimiento.

El ciclo es **lanzar → esperar → seguir al pez → comprobar el cierre del menú → volver a lanzar**. El contador registra rondas terminadas, tanto si hubo captura como si escapó el pez.

## Descargar y empezar

Extrae toda la carpeta del ZIP `SomeFishing-GPO-v0.2.0-win-x64.zip` y abre `SomeFishingGPO.exe`. Requiere Windows 10 u 11 de 64 bits, .NET Framework 4.8 y el cliente de escritorio de Roblox. La lectura de cebo utiliza el reconocimiento de texto de Windows y necesita al menos un idioma OCR disponible para tu perfil. La aplicación no descarga ni instala idiomas. El SDK solo es necesario para recompilar, no para ejecutar el binario entregado.

1. Abre Roblox en ventana o sin bordes, equipa la caña y lanza una vez manualmente.
2. Con el minijuego visible, pulsa **F6**. Dibuja **una sola zona** con toda la altura de la barra azul y sus dos bordes oscuros. Deja margen lateral para su pequeño balanceo. La barra verde puede quedar dentro.
3. Confirma con **Enter o F6**. **Esc** cancela sin perder la selección anterior.
4. Pulsa **Ver detector**: el contorno celeste debe seguir la barra azul, naranja marca el hueco y rosa marca el pez. Esta vista no envía clics. Mantén la ventana de la macro fuera de la zona del juego que captura.
5. Selecciona **Elegir punto sobre el agua**. Marca **Permitir clics y saltos al iniciar**.
6. Vuelve a Roblox y pulsa **F8**. **F10** detiene la macro; F8 también alterna inicio y parada.

La selección debe cubrir el recorrido de la barra. Incluye algo de margen vertical, pero procura que la barra ocupe la mayor parte de la altura. Se admiten hasta 1200 px de ancho, 1400 px de alto y 600 000 píxeles en total.

![Ejemplo sintético de una sola zona con margen lateral](docs/Selector-zona.png)

## Leer el cebo y saltar durante la espera

Estas funciones empiezan desactivadas y se configuran en la pestaña **Cebo y espera**.

1. Pulsa **Seleccionar contador de cebo** y dibuja una **segunda zona pequeña**, únicamente alrededor del número del cebo equipado, por ejemplo `x300`. Incluye un poco de margen para que quepan más dígitos. Confirma con Enter o F6. No selecciones el nombre, los dos tipos de cebo ni la barra verde. La zona del minijuego se conserva.
2. Activa **Probar lectura · sin teclas**. Comprueba que el número mostrado coincide con el juego antes de iniciar la macro. La vista de prueba no envía clics ni saltos. El contador debe permanecer visible en esa posición.
3. Marca **Saltar en espera si el cebo llega a 0 o fallan 3 lanzamientos**. El intervalo inicial es **60 segundos**; puedes ajustarlo entre 15 y 300 segundos.
4. Pulsa **Guardar ajustes**, termina la prueba de lectura, vuelve a Roblox y usa F8 para iniciar con el permiso de clics y saltos marcado.

![Configuración de lectura del cebo y saltos](docs/Cebo-y-espera.png)

El lector procesa únicamente la zona del contador con OCR local de Windows, en un segundo plano. Exige que dos tamaños de la imagen produzcan el mismo número. Además, confirma los números positivos en dos capturas diferentes y el cero en tres. Una imagen ausente, ilegible o ambigua se muestra como **desconocida**, nunca se convierte automáticamente en cero. No suma los dos tipos de cebo ni cambia entre ellos.

**Límite observado:** en la captura de desarrollo reconoce los **300 cebos comunes**, pero no reconoce con fiabilidad los **42 raros**. Otros tamaños, fondos y dígitos también requieren comprobar la lectura. Si no reconoce tu contador, puedes usar los saltos tras tres lanzamientos sin minijuego con la lectura de cebo desactivada.

| Situación | Comportamiento |
|---|---|
| El minijuego se cierra después de una ronda | Espera la pausa habitual y vuelve a lanzar; ese cierre por sí solo no activa saltos |
| Se confirma que quedan 0 cebos | Termina el minijuego activo; si ya lanzó, conserva la espera de la última picada. Después entra en espera |
| Fallan tres lanzamientos sin aparecer el minijuego | Entra en espera aunque el contador sea desconocido |
| Entra en espera con saltos habilitados | Suelta el clic y solicita un salto tras unos 2 segundos sin menú; después repite al intervalo configurado |
| Reaparece el minijuego durante la espera | Suspende los saltos y reanuda el seguimiento cuando confirma la detección |
| Se repone cebo tras una espera causada por cero confirmado | Reanuda los lanzamientos cuando confirma una cantidad positiva |
| La espera comenzó por tres lanzamientos fallidos | Permanece en espera hasta que reaparezca el minijuego o reinicies con F8; no reintenta indefinidamente |
| Los saltos están desactivados | Se detiene cuando se confirma la falta de cebo o fallan los tres intentos |

Cada salto consiste en una pulsación breve de **Espacio**, sin teclas de dirección. **F10, F8, cambiar de ventana o llevar el ratón a la esquina superior izquierda detiene también los saltos**. La macro no comprueba que el personaje haya saltado ni que el juego contabilice esa pulsación como actividad. Esta función no guarda objetos ni protege frente a caídas de conexión, del servidor o del proceso.

## Ajustes iniciales

| Ajuste | Valor | Función |
|---|---:|---|
| Mantener al lanzar | 220 ms | Duración del clic de lanzamiento |
| Espera de picada | 15 s | Tiempo antes de intentar otro lanzamiento |
| Pausa entre rondas | 1800 ms | Espera después de confirmar el cierre del menú |
| Tolerancia de color | 38 | Margen de reconocimiento del azul y el blanco |
| Anticipación | 80 ms | Compensación del movimiento; el frenado aumenta al estar el pez casi quieto |

Las zonas, el punto y los ajustes se guardan en `ajustes.xml`. La autorización para enviar clics y saltos se desmarca al volver a abrir el programa. Los ajustes de versiones anteriores conservan la lectura de cebo y los saltos desactivados. Si cambias la posición, resolución o escala del juego, revisa ambas selecciones.

**Ver última parada** muestra el motivo de la última interrupción y lo conserva en `ultima-parada.txt`, junto con la última cantidad confirmada de cebo y el número de pulsaciones de salto solicitadas. Tres segundos de detección incompleta con menú visible o una ronda de más de dos minutos detienen la macro. Las paradas de protección no activan los saltos de espera.

## Compilar y comprobar

El proyecto usa C# 5, .NET Framework 4.8 y la API de OCR de Windows. Para compilar necesitas el **Windows 10 o Windows 11 SDK**, además del compilador de .NET Framework. El script no descarga dependencias y avisa si faltan. Para el OCR nativo se necesita un idioma compatible disponible en el perfil de Windows; consulta la [documentación de Microsoft](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine.trycreatefromuserprofilelanguages?view=winrt-26100).

```bat
compilar.cmd
SomeFishingGPO.exe --self-test pruebas
```

`compilar.cmd` utiliza `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, busca los metadatos `Windows.winmd` del SDK y las bibliotecas locales de interoperabilidad. El modo de prueba no registra atajos globales ni envía clics o teclas reales. Guarda el informe en `pruebas/resultados.txt`; el código de salida 0 indica éxito.

Las **162 comprobaciones incluidas** cubren imágenes sintéticas, seguimiento, balanceo lateral, exclusión del verde, confirmación del contador, estados de espera, regreso a la pesca y fallos simulados del clic y de Espacio. La validación local alcanzó **213 comprobaciones** al añadir cuatro capturas del desarrollo, incluida la del cebo; esas capturas no se distribuyen. Tres comprobaciones adicionales ejercitan el OCR nativo sobre esa captura. Los resultados de esta compilación y el análisis de Defender están en [VERIFICACION.txt](docs/VERIFICACION.txt).

Puedes comparar la huella del ejecutable descargado con [SHA256.txt](docs/SHA256.txt):

```powershell
Get-FileHash .\SomeFishingGPO.exe -Algorithm SHA256
```

La huella corresponde al binario entregado. Una recompilación local puede producir otra huella por los metadatos generados por el compilador; no se afirma que la compilación sea reproducible byte por byte.

## Código y transparencia

| Archivo | Responsabilidad |
|---|---|
| `src/Core.cs` | Localización visual, controlador, configuración y ciclo de pesca |
| `src/Native.cs` | Captura, foco de Roblox, clics y pulsaciones de Espacio |
| `src/App.cs` | Interfaz, zona, vista previa y teclas |
| `src/Bait.cs` | Interpretación estricta del número y confirmación de lecturas |
| `src/WindowsBaitReader.cs` | OCR local de Windows en segundo plano |
| `src/Tests.cs` | Pruebas con imágenes y entrada simulada |
| `src/AssemblyInfo.cs` | Nombre y versión del ejecutable |
| `src/app.manifest` | Ejecución sin elevación administrativa |

El programa no tiene telemetría, funciones de red, lectura de credenciales ni cambios en el antivirus. Las capturas se procesan en memoria. Consulta [SECURITY.md](SECURITY.md) para los detalles y límites de las protecciones.

El código se desarrolló con asistencia de IA y se entrega para revisión; los resultados de pruebas no sustituyen una auditoría independiente. La detección puede fallar con otros colores, fondos, tamaños, movimientos o latencias. Este proyecto no está afiliado a Roblox ni a los creadores de GPO.

## Licencia

**Sin licencia concedida por ahora**, por decisión del titular del repositorio. El código se publica para revisión. Consulta [NOTICE.md](NOTICE.md).
