# Recopilador temporal de referencias

Herramienta separada basada en SomeFishing GPO 0.9.1. Abre **CapturarReferencias.exe**. El motor de pesca y los controladores de entrada originales se conservan; este ejecutable coordina capturas entre rondas y no utiliza el flujo de compra.

## Preparación

1. Cierra la macro normal para liberar F8/F10.
2. Sitúate junto al vendedor de cebo común, con la caña preparada, el agua accesible y los diálogos cerrados. Mantén tu resolución y escala habituales.
3. Comprueba que tienes **30 cebos comunes reales**. Si tienes otra cantidad entre 1 y 30, escríbela en la herramienta.
4. Los puntos y tiempos se cargan de la copia personal incluida. Si cambiaste tu calibración, usa **Cargar ajustes…** y selecciona el `ajustes.xml` de tu macro habitual. No se modifica ese archivo.
5. Marca la confirmación, pulsa **Iniciar** y vuelve a Roblox antes de tres segundos. También puedes iniciar con **F8** después de marcar la confirmación.

**F10 detiene.** F8 también detiene una sesión activa. Cambiar de ventana detiene la recopilación, igual que en la macro original. No muevas el ratón durante los clics automáticos.

## Qué hace

- Selecciona cebo común usando tu punto guardado.
- Antes del primer lanzamiento, guarda dos capturas del contador.
- Mantiene E, espera, mueve gradualmente el puntero a Sí y abre el diálogo de cantidad.
- Guarda dos capturas de la pantalla que contiene MAX, sin leer ni editar ese número.
- Pulsa Cancelar. Si aparece un diálogo con tres puntos, lo cierra.
- Comprueba el cierre y continúa con una ronda de pesca del motor original.
- Repite después de cada ronda terminada. Un lanzamiento sin minijuego no consume una muestra ni descuenta una ronda.

No escribe cantidades ni pulsa Comprar. Como Sí y Comprar comparten coordenadas, el clic izquierdo requiere imágenes nuevas del aspecto de Sí/No justo antes de pulsar. Esta comprobación usa los colores y la disposición de los botones; no usa OCR. Si no puede reconocer el paso dentro del plazo, se detiene en lugar de continuar con clics fuera de fase. El registro y, si es posible, una captura `interrumpido` ayudan a revisar el problema.

Con 30 cebos declarados recopila **30 series**: una inicial y otra tras cada una de hasta **29 rondas**. Si no hay devoluciones, corresponden a 30 → 1. Con 1 cebo declarado solo toma la serie inicial, sin pescar. Cada ronda tiene un límite de tres minutos, cada etapa del diálogo de quince segundos y la sesión de dos horas.

## Imágenes y revisión

El botón **Ver capturas** abre la sesión más reciente. Las carpetas se guardan junto al ejecutable:

```text
Referencias/
  fecha-hora-identificador/
    muestra-000-cebos-a.png
    muestra-000-cebos-b.png
    muestra-000-max-a.png
    muestra-000-max-b.png
    ...
    revisar.csv
    resultado.txt
```

Los PNG completos conservan la resolución del área cliente de Roblox. También se guardan recortes del contador y del menú de cebos cuando sus zonas están configuradas dentro de la ventana. Las dos imágenes de cada etapa se toman separadas por al menos 300 ms.

**Las imágenes no están etiquetadas con una cantidad real.** `revisar.csv` separa `cebos_estimados` de las columnas vacías `cebos_reales_revisados` y `max_revisado`. La estimación se basa en lo que declaraste y las rondas detectadas; no es OCR. Revisa las fotos antes de completar esas columnas.

Si el tiburón devuelve cebo, se guardarán cantidades repetidas y la última serie puede mostrar más de 1. No se descartan duplicados ni se continúa pescando a ciegas para compensarlos. Para completar referencias faltantes, inicia otra sesión indicando la cantidad real que ves. Cada inicio crea otra carpeta y no sobrescribe fotos anteriores.

El valor inicial esperado de MAX con 30 cebos es 270 **si ese límite depende únicamente del espacio hasta 300**. La herramienta no presupone esa relación ni deduce inventario a partir de MAX.

Conserva las carpetas de imágenes para enviarlas o revisarlas. Cuando terminemos de preparar las referencias, ya no necesitarás este ejecutable; no añade ninguna función a tu macro normal. Las capturas permanecen locales, no se suben automáticamente, y pueden incluir nombres o chat visibles dentro del juego.

## Código y pruebas

`compilar-referencias.cmd` compila el recopilador con el compilador local y las mismas dependencias de .NET Framework 4.8 y Windows SDK que la macro. No descarga paquetes. El punto de entrada es `ReferenceCollectorProgram`; `Program` y los demás archivos originales se conservan como base.

```text
CapturarReferencias.exe --self-test carpeta-de-resultados
```

Pruebas sin entradas reales: 1.305 comprobaciones de la base y 59 del recopilador. Incluyen la serie completa, reserva, capturas iniciales, ausencia de compra/teclas numéricas, cancelación y pérdida de foco en cada etapa, menú cambiado antes de Sí, fallos de captura, lecturas visuales antiguas y archivos sin sobreescritura.

Se comprobó la interfaz mediante un render fuera de pantalla. Estas pruebas no verifican la respuesta de los menús en una partida real. La copia habitual 0.9.1 y sus ajustes permanecen intactos. Se conserva `NOTICE.md`, sin conceder una licencia.

