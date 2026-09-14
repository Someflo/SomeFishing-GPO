# SomeFishing GPO

Macro visual de pesca para GPO en Windows, con código disponible para revisar y compilar. **Versión local 0.8.1.** [English guide](README.en.md).

El selector **Idioma / Language**, visible en la barra lateral, cambia entre Español y English y guarda la elección. El idioma OCR se configura por separado.

## Empezar

Extrae el ZIP completo y abre `SomeFishingGPO.exe`. Requiere Windows 10/11 de 64 bits y .NET Framework 4.8. No necesita administrador. Conserva `ajustes.xml` al actualizar.

1. En **Cebos**, escribe cuántos legendarios, raros y comunes tienes. Usa 0 para un tipo agotado y pulsa **Aplicar inventario**. Este paso es obligatorio antes de iniciar pesca con entradas.
2. En **Cebos → Botones de cebo**, marca el centro de las filas **Legendario**, **Raro** y **Común** que usarás. Deja el menú en su posición habitual; común también necesita un punto si vas a comprarlo.
3. Equipa la caña y lanza una vez manualmente. Con **F6**, selecciona toda la barra azul y deja margen para el movimiento lateral. Enter o F6 confirma; Esc cancela.
4. En **Zonas**, marca el punto del agua y, si vas a comprar, los tres botones del diálogo. Cierra los diálogos antes de iniciar.
5. Elige la reposición en **Inicio**, prueba los puntos en **Pruebas**, termina cualquier ronda abierta, activa **Permitir clics y teclas**, vuelve a Roblox y pulsa **F8**.

**F10 detiene**. F8 durante la ejecución, cambiar de ventana o llevar el ratón a la esquina superior izquierda también detiene y libera las entradas. Iniciar desde la aplicación deja tres segundos para volver al juego.

## Inventario y rondas

La prioridad es **legendario → raro → común**, conservando **1 de cada tipo** para que sus filas no desaparezcan. Solo usa un tipo si la cantidad estimada es mayor que 1: con 20 legendarios usa 19 y pasa a raro. Repone común antes de gastar su última unidad; si no puede comprar, conserva esa reserva.

Para elegir el tipo, mueve el puntero y pulsa el punto que marcaste. **No necesita reconocer nombres ni bordes amarillos, pero tampoco verifica qué tipo quedó seleccionado.** Comprueba los puntos en el juego y márcalos de nuevo si cambia el menú, la ventana o la resolución. Los tres puntos de cebo son distintos de los tres botones del diálogo de compra.

La cuenta es una **estimación** basada en tus cantidades: descuenta un cebo del tipo activo cuando confirma el inicio de un minijuego. No descuenta por un clic de lanzamiento que nunca produce minijuego, ni vuelve a descontar por observar varias imágenes de la misma ronda. El contador de rondas terminadas incluye capturas y escapes; **no es un contador de peces capturados**. Los lanzamientos se muestran por separado.

Las cantidades estimadas se guardan con los ajustes, en segundo plano para no interrumpir el control por una escritura lenta. Corrígelas y aplica el inventario si usaste cebos fuera de la macro, detectas una diferencia o una compra queda dudosa. El programa no puede conocer un gasto ocurrido cuando no estaba observando.

El OCR es opcional y sirve de apoyo: compara el contador de la fila activa con la estimación y muestra diferencias. Para usarlo, activa **Avanzado → OCR de apoyo al inventario** y selecciona **Zonas → Menú OCR · opcional**. No reemplaza tus cantidades ni convierte una lectura desconocida en cero. Su idioma se elige por separado entre los de Windows instalados; el procesamiento es local y las esperas del lector tienen un plazo máximo.

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
- **Cronómetro:** solicita 50 comunes cada 40 minutos, ambos ajustables y limitados al espacio de la capacidad configurada. Espera a estar usando común; si solo queda la unidad reservada, puede reponer antes del intervalo sin gastarla.

La compra espera el cierre de la ronda y ejecuta **E → Sí → doble clic en el número → escribir cantidad → Comprar → …**. Un cronómetro que vence durante un lanzamiento pendiente espera a identificar esa ronda o agotar la espera de picada, para no perder su descuento de cebo. Al terminar la compra vuelve gradualmente al agua, espera a estabilizar el puntero y reanuda la pesca.

## Sesiones largas y recuperación

**Inicio → Recuperar fallos temporales** viene activado al configurar esta versión por primera vez. Permite esperar y reintentar tras fallos recuperables, sin depender de los saltos. En **Avanzado → Sesiones largas**, ajusta el máximo de fallos seguidos y la pausa inicial: 3 y 20 segundos por defecto. Las pausas aumentan si vuelve a fallar; una ronda terminada o una compra completada reinicia la cuenta de fallos seguidos.

Revisa **Avanzado → Tope de compras** para la duración que necesitas. Con recuperación activada, el tope cuenta órdenes enviadas; un fallo antes de pulsar Comprar no lo consume. No se aumenta automáticamente al agotarlo. Inicio muestra tiempo, compras y recuperaciones.

En **Avanzado → Recuperación** puedes cambiar los reintentos sin minijuego, los reintentos por fase de compra y la espera máxima por fase. Los valores iniciales son 2, 2 y 10 segundos. El aviso de Avanzado recomienda conservar los ajustes si no los conoces.

La compra diferencia la pregunta Sí/No, el menú de cantidad, el cierre con tres puntos y la ausencia de diálogo mediante imágenes nuevas de los tres puntos. Reintenta abrir únicamente si no hay diálogo, Sí si sigue la pregunta, y el cierre si siguen los tres puntos. Una acción atrasada vuelve a comprobar el menú antes de enviarse. Además de los reintentos, la compra tiene un límite global de 90 segundos y la recuperación también queda acotada; **Comprar no se envía de nuevo una vez intentado**, incluso si la llamada de entrada devuelve un error.

Si una fase falla, intenta identificar el menú: pulsa No/Cancelar en la pregunta o la cantidad, o cierra los tres puntos. Solo vuelve a pescar después de observar el diálogo cerrado. Con recuperación activada puede esperar y repetir esta limpieza hasta el límite configurado; también recupera los lanzamientos sin minijuego. No sigue enviando clics indefinidamente ante el mismo fallo.

El cierre observado permite sumar la cantidad solicitada a la **estimación**, pero no demuestra que el juego aceptara ese número ni que entregara todos los cebos. Si se envió Comprar y el resultado quedó incierto, no acredita cebos ni repite el pedido: cierra el diálogo si puede y te pide corregir el inventario antes de continuar.

## Pruebas y límites

**Probar compra** realiza una sola compra de la cantidad indicada, sin esperar OCR, umbral ni cronómetro. **Usa Peli del juego** y no inicia pesca al terminar. **Probar sin cebo** simula agotamiento para probar la acción configurada. Ambas pruebas necesitan permiso de entradas y Roblox en primer plano; F10 cancela. El registro queda en `ultima-prueba.txt`.

La pesca busca los bordes reales de la barra azul, en lugar de exigir que ocupe un porcentaje fijo de la altura seleccionada. Sigue la línea blanca y el hueco gris dentro de esos bordes y descarta la barra verde. Puedes dejar margen para el balanceo; si encuentra varias barras, pide reducir la zona.

Una pérdida breve de imagen conserva el control hasta 180 ms desde la última detección válida. Si dura más, libera el clic; no inventa una posición del pez ni descuenta otro cebo. Con recuperación activada, una detección incompleta prolongada pasa a esperar imágenes nuevas de la barra o su cierre, hasta 90 segundos. Si reaparece, continúa la misma ronda; si no puede recuperarla, se detiene. Cambiar de ventana o una compra dudosa siguen requiriendo intervención.

La versión incluye **32 comprobaciones de sesiones aceleradas de 24 y 12 horas**, con pérdidas de imagen, fallos de lanzamiento, OCR incorrecto y compras con menús atrasados. Comprueban el inventario independiente, la reserva y la ausencia de pedidos duplicados. **No son horas reales de Roblox ni prueban la entrega de entradas nativas.**

Los saltos de espera son opcionales. El reconocimiento depende de la escala, los colores, el fondo y la colocación de las zonas; las simulaciones no demuestran que Roblox haya recibido una entrada. No se garantiza una tasa de captura, evitar desconexiones ni conservar objetos.

## Compilar y revisar

Ejecuta `compilar.cmd` con .NET Framework 4.8 y Windows 10/11 SDK instalados. Usa el compilador local y no descarga paquetes. El SDK no es necesario para ejecutar el binario distribuido.

`SomeFishingGPO.exe --self-test carpeta-de-resultados` ejecuta comprobaciones con entradas simuladas. No envía teclas ni clics al juego. Los resultados concretos de cada compilación se documentan en `docs/VERIFICACION.txt`.

- `src/Core.cs`, `src/EngineInventory.cs`, `src/ManualInventory.cs`, `src/LongSession.cs`: pesca, inventario, reserva y recuperación.
- `src/BaitSelection.cs`: detección de filas para el OCR de apoyo; la selección habitual usa puntos marcados.
- `src/TrackingDetector.cs`: bordes de la barra, hueco gris, línea blanca y búsqueda alrededor de la última barra válida.
- `src/DirectPurchase.cs`, `src/ShopVisual.cs`: compra por puntos, identificación de menús y recuperación.
- `src/RelativePointer.cs`, `src/Native.cs`: movimiento, captura, entradas de Windows y liberación.
- `src/WindowsBaitReader.cs`, `src/CounterGlyphs.cs`, `src/WinRtWait.cs`: lectura local de apoyo con esperas limitadas.
- `src/AsyncSettingsStore.cs`: guardado en segundo plano con una única escritura activa y la última actualización pendiente.
- `src/App.cs`, `src/Interface.cs`, `src/InventoryInterface.cs`: interfaz y selección de zonas.
- `src/Localization.cs`, `src/Strings.en.tsv`: traducciones incluidas en el ejecutable.

El programa no usa red, descarga actualizaciones ni lee la memoria del juego. Un antivirus sin detecciones no constituye una garantía absoluta. Consulta [SECURITY.md](SECURITY.md). El código se publica **sin conceder una licencia por ahora**; se conserva [NOTICE.md](NOTICE.md).
