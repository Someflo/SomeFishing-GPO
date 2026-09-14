# SomeFishing GPO

Macro visual de pesca para GPO en Windows, con código disponible para revisar y compilar. **Versión local 0.8.0.** [English guide](README.en.md).

El selector **Idioma / Language**, visible en la barra lateral, cambia entre Español y English y guarda la elección. El idioma OCR se configura por separado.

## Empezar

Extrae el ZIP completo y abre `SomeFishingGPO.exe`. Requiere Windows 10/11 de 64 bits y .NET Framework 4.8. No necesita administrador. Conserva `ajustes.xml` al actualizar.

1. En **Cebos**, escribe cuántos legendarios, raros y comunes tienes. Usa 0 para un tipo agotado y pulsa **Aplicar inventario**. Este paso es obligatorio antes de iniciar pesca con entradas.
2. Pulsa **Seleccionar menú completo** y rodea todas las filas de cebo, con margen. No marques posiciones fijas para cada fila.
3. Equipa la caña y lanza una vez manualmente. Con **F6**, selecciona toda la barra azul y deja margen para el movimiento lateral. Enter o F6 confirma; Esc cancela.
4. En **Zonas**, marca el punto del agua y, si vas a comprar, los tres botones del diálogo. Cierra los diálogos antes de iniciar.
5. Elige la reposición en **Inicio**, prueba los puntos en **Pruebas**, termina cualquier ronda abierta, activa **Permitir clics y teclas**, vuelve a Roblox y pulsa **F8**.

**F10 detiene**. F8 durante la ejecución, cambiar de ventana o llevar el ratón a la esquina superior izquierda también detiene y libera las entradas. Iniciar desde la aplicación deja tres segundos para volver al juego.

## Inventario y rondas

La prioridad es **legendario → raro → común**. El programa localiza de nuevo las filas por su aspecto cuando selecciona otro tipo. Si una fila desaparece, no desplaza ciegamente un índice guardado: busca la siguiente fila dentro del menú completo y comprueba su borde amarillo antes de pescar. Una fila dudosa detiene la selección.

La cuenta es una **estimación** basada en tus cantidades: descuenta un cebo del tipo activo cuando confirma el inicio de un minijuego. No descuenta por un clic de lanzamiento que nunca produce minijuego, ni vuelve a descontar por observar varias imágenes de la misma ronda. El contador de rondas terminadas incluye capturas y escapes; **no es un contador de peces capturados**. Los lanzamientos se muestran por separado.

Las cantidades estimadas se guardan con los ajustes. Corrígelas y aplica el inventario si usaste cebos fuera de la macro, detectas una diferencia o una compra queda dudosa. El programa no puede conocer un gasto ocurrido cuando no estaba observando.

El OCR es opcional y sirve de apoyo: compara el contador de la fila activa con la estimación y muestra diferencias. No reemplaza automáticamente las cantidades que escribiste, ni convierte una fila oculta o una lectura desconocida en cero. Su idioma se elige en **Avanzado** entre los idiomas de Windows instalados; todo el procesamiento es local.

El recorte de apoyo sigue el contador de la fila activa y separa sus caracteres del borde amarillo. Esto mejora la imagen entregada al lector; no garantiza que Windows OCR reconozca cada número.

## Compra de cebo común

La compra automática repone **solo cebo común**. Sitúa al personaje al alcance de **E** junto al barril y desde un punto donde pueda pescar. Marca el centro del texto de los tres botones:

| Punto | Uso |
|---|---|
| Izquierdo | Sí y Comprar |
| Central | Número editable y cierre «…» |
| Derecho | No / Cancelar, también usado para salir durante la recuperación |

Los puntos deben mantener su posición entre diálogos. Márcalos de nuevo si cambia la ventana, resolución o disposición. El clic del selector se hace sobre una captura y no se envía al juego.

En **Inicio**, activa **Comprar cebo** y elige:

- **Inventario y rondas:** cuando queden 2 comunes o menos, solicita completar hasta 300. Con 2 solicita 298. Umbral y capacidad son ajustables. No lee el MAX del diálogo ni el saldo disponible.
- **Cronómetro:** solicita 50 comunes cada 40 minutos, ambos ajustables y limitados al espacio de la capacidad configurada. Espera a estar usando común; si se agota el inventario, puede reponer antes del intervalo para no quedar esperando sin cebo.

La compra espera el cierre de la ronda y ejecuta **E → Sí → doble clic en el número → escribir cantidad → Comprar → …**. Mantiene las pausas y mueve el puntero gradualmente. Al terminar vuelve al punto del agua, espera a que llegue y se estabilice, y reanuda la pesca.

## Recuperación de fallos

En **Avanzado → Recuperación** puedes cambiar los reintentos sin minijuego, los reintentos por fase de compra y la espera máxima por fase. Los valores iniciales son 2, 2 y 10 segundos. El aviso de Avanzado recomienda conservar los ajustes si no los conoces.

La compra diferencia la pregunta Sí/No, el menú de cantidad, el cierre con tres puntos y la ausencia de diálogo mediante imágenes nuevas de los tres puntos. Reintenta abrir únicamente si no hay diálogo, Sí si sigue la pregunta, y el cierre si siguen los tres puntos. Una acción atrasada vuelve a comprobar el menú antes de enviarse. Además de los reintentos, la compra tiene un límite global de 90 segundos y la recuperación también queda acotada; **Comprar no se envía de nuevo una vez intentado**, incluso si la llamada de entrada devuelve un error.

Si una fase falla, intenta identificar el menú: pulsa No/Cancelar en la pregunta o la cantidad, o cierra los tres puntos. Solo intenta volver a pescar después de observar el diálogo cerrado. Si no distingue el menú o no consigue cerrarlo, se detiene. Tras agotar los intentos sin minijuego también puede probar una recuperación del menú antes de volver a lanzar.

El cierre observado permite sumar la cantidad solicitada a la **estimación**, pero no demuestra que el juego aceptara ese número ni que entregara todos los cebos. Si se envió Comprar y el resultado quedó incierto, no acredita cebos ni repite el pedido: cierra el diálogo si puede y te pide corregir el inventario antes de continuar.

## Pruebas y límites

**Probar compra** realiza una sola compra de la cantidad indicada, sin esperar OCR, umbral ni cronómetro. **Usa Peli del juego** y no inicia pesca al terminar. **Probar sin cebo** simula agotamiento para probar la acción configurada. Ambas pruebas necesitan permiso de entradas y Roblox en primer plano; F10 cancela. El registro queda en `ultima-prueba.txt`.

La pesca busca los bordes reales de la barra azul, en lugar de exigir que ocupe un porcentaje fijo de la altura seleccionada. Sigue la línea blanca y el hueco gris dentro de esos bordes y descarta la barra verde. Puedes dejar margen para el balanceo; si encuentra varias barras, pide reducir la zona.

Una pérdida breve de imagen conserva el control hasta 180 ms desde la última detección válida. Si dura más, libera el clic; no inventa una posición nueva del pez. Esa pérdida breve no inicia otra ronda ni vuelve a descontar cebo. Un menú visible pero incompleto acaba deteniendo la pesca si no se recupera.

Los saltos de espera son opcionales. El reconocimiento depende de la escala, los colores, el fondo y la colocación de las zonas; las simulaciones no demuestran que Roblox haya recibido una entrada. No se garantiza una tasa de captura, evitar desconexiones ni conservar objetos.

## Compilar y revisar

Ejecuta `compilar.cmd` con .NET Framework 4.8 y Windows 10/11 SDK instalados. Usa el compilador local y no descarga paquetes. El SDK no es necesario para ejecutar el binario distribuido.

`SomeFishingGPO.exe --self-test carpeta-de-resultados` ejecuta comprobaciones con entradas simuladas. No envía teclas ni clics al juego. Los resultados concretos de cada compilación se documentan en `docs/VERIFICACION.txt`.

- `src/Core.cs`, `src/EngineInventory.cs`, `src/ManualInventory.cs`: pesca, rondas, inventario estimado y reposición.
- `src/BaitSelection.cs`: reconocimiento de filas y confirmación del tipo seleccionado.
- `src/TrackingDetector.cs`: bordes de la barra, hueco gris, línea blanca y búsqueda alrededor de la última barra válida.
- `src/DirectPurchase.cs`, `src/ShopVisual.cs`: compra por puntos, identificación de menús y recuperación.
- `src/RelativePointer.cs`, `src/Native.cs`: movimiento, captura, entradas de Windows y liberación.
- `src/WindowsBaitReader.cs`, `src/CounterGlyphs.cs`: lectura local de apoyo.
- `src/App.cs`, `src/Interface.cs`, `src/InventoryInterface.cs`: interfaz y selección de zonas.
- `src/Localization.cs`, `src/Strings.en.tsv`: traducciones incluidas en el ejecutable.

El programa no usa red, descarga actualizaciones ni lee la memoria del juego. Un antivirus sin detecciones no constituye una garantía absoluta. Consulta [SECURITY.md](SECURITY.md). El código se publica **sin conceder una licencia por ahora**; se conserva [NOTICE.md](NOTICE.md).
