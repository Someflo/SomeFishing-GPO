# Seguridad y transparencia

SomeFishing GPO 0.8.0 funciona localmente: captura las zonas elegidas, analiza píxeles y envía entradas normales de Windows. No lee ni modifica la memoria de Roblox, inyecta código, descarga archivos, recoge credenciales, cambia el antivirus ni requiere administrador. No tiene funciones de red, inicio automático ni actualizador.

## Entradas y datos

El selector Español/English traduce la presentación y guarda `InterfaceLanguage` en `ajustes.xml`. Las traducciones están incluidas en el ejecutable. No cambia el idioma OCR, la cultura de Windows, las coordenadas ni las teclas enviadas.

El seguimiento usa clic sostenido y un punto de lanzamiento elegido por el usuario. Antes de lanzar, incluso después de comprar, mueve gradualmente el puntero al agua, comprueba llegada y estabilidad y espera 200 ms adicionales. La duración del lanzamiento empieza desde la pulsación. Si aparece el minijuego durante ese movimiento, cancela el lanzamiento pendiente.

El detector delimita la barra por sus bordes reales y busca pez y hueco dentro de ella; no exige un porcentaje fijo de altura de la zona. La última barra válida puede orientar una nueva búsqueda, pero no se devuelve su posición antigua como observación actual. Si falta una detección, conserva la última decisión de control hasta 180 ms y después libera el clic. Una interrupción breve mantiene la identidad de la ronda para evitar descuentos duplicados.

Los saltos opcionales usan Espacio sin direcciones. Las compras usan E, los puntos marcados, Ctrl+A, Retroceso y dígitos, y pueden gastar Peli del juego. La selección de cebo añade clics dentro del menú completo autorizado. La recuperación puede pulsar No/Cancelar o cerrar los tres puntos. No se leen textos del portapapeles ni se escriben textos libres.

F6/F8/F10 controlan selección, inicio y parada. No se registra lo escrito en otras aplicaciones. Antes de cada entrada se comprueba el foco de Roblox y que el destino esté dentro de su ventana. La ventana bajo el cursor también debe corresponder a Roblox; si el cursor se aparta del destino, el clic se cancela.

Los ajustes y registros se guardan junto al ejecutable: `ajustes.xml`, `ultima-parada.txt`, `ultima-prueba.txt`. Incluyen las cantidades manuales y su estado de incertidumbre. Las capturas normales se procesan en memoria. Los registros pueden contener cantidades, coordenadas, fragmentos de OCR, nombres de proceso e identificadores de ventana; no títulos, rutas ni capturas. Copiar resultado escribe al portapapeles únicamente cuando se pulsa ese botón.

## Inventario estimado y selección de tipos

Antes de iniciar pesca con entradas, el usuario debe escribir y confirmar las cantidades de común, raro y legendario. El programa aplica prioridad legendario → raro → común. No obtiene el inventario de la memoria del juego.

Un minijuego confirmado descuenta una unidad estimada del tipo activo. Repetir imágenes de esa ronda no vuelve a descontar; un lanzamiento sin minijuego no descuenta. El final de una ronda tampoco afirma captura: puede haber terminado en escape. El inventario necesita corrección si hubo gasto fuera de la macro, una ronda no fue detectada o el juego no respondió como se esperaba.

La selección observa el rectángulo completo marcado por el usuario y localiza las filas por aspecto y colores. Requiere evidencia nueva y estable, mueve el puntero a la fila encontrada y comprueba su borde amarillo. Si una fila desaparece, no usa un índice fijo desplazado. Un menú desconocido, un destino movido o un tipo sin confirmación impiden continuar. La ausencia visual no se convierte en una cantidad cero.

El OCR opcional compara el contador de la fila activa con el inventario estimado. Al cambiar de tipo, descarta resultados pendientes del lector anterior para no mezclar cantidades. El OCR no reemplaza por sí solo lo escrito por el usuario ni confirma un pedido. Usa Windows OCR local y referencias pequeñas de x2/x3/x4; no descarga idiomas.

El recorte del contador se reconstruye a partir de los caracteres de la fila y excluye el borde amarillo. La geometría del recorte y la lectura numérica son comprobaciones distintas: aislar un contador no demuestra que el OCR haya reconocido su cantidad.

## Compra y recuperación por puntos

El izquierdo corresponde a Sí/Comprar; el central, al número y cierre; el derecho, a No/Cancelar. Este último puede pulsarse durante la recuperación de un menú bloqueado. No se derivan destinos de porcentajes del diálogo ni se exige OCR para leer sus menús. Los puntos deben conservar sus posiciones entre fases.

El movimiento utiliza desplazamientos relativos y corrige el recorrido con la posición que devuelve Windows. No modifica la configuración global del ratón. Exige llegada, estabilidad y la pausa elegida antes de pulsar. Las pulsaciones van separadas del movimiento. La posición del cursor y la aceptación de la entrada por Windows no demuestran que el juego haya procesado el clic.

Se observan tres parches de 49 × 33 píxeles alrededor de los puntos. Distinguen la pregunta por sus etiquetas blancas laterales, cantidad por verde/blanco/rojo, y cierre por tres componentes blancos pequeños alineados en el centro. Si la evidencia se contradice, el menú queda desconocido. Para avanzar se requieren al menos dos capturas nuevas separadas 100 ms. Una imagen repetida, futura o de más de 500 ms no autoriza una entrada. Las acciones que quedaron atrasadas deben renovar su confirmación.

Los reintentos de compra son ajustables de 0 a 5, además de la primera acción, y el tiempo de fase de 3 a 60 segundos. La secuencia y la recuperación tienen límites globales de tiempo. Solo se reintenta E con ausencia estable, Sí con la pregunta presente y el cierre con los tres puntos presentes. **Comprar se marca enviado antes de llamar al controlador y no se vuelve a intentar**, aunque la llamada lance una excepción después de haber enviado parte de la entrada.

Tras una fase fallida, una secuencia independiente identifica el menú y cancela con el botón derecho o cierra los tres puntos. No abre E, escribe cantidades ni compra durante esa limpieza. Solo permite regresar a la preparación de pesca tras observar ausencia estable. Si persiste un menú o no puede identificarse, detiene el proceso.

La reposición manual solicita completar la capacidad de común al alcanzar el umbral. El cronómetro solicita su cantidad fija, limitada al espacio estimado. No se lee saldo, MAX ni aceptación del número. La ronda activa termina antes de comenzar una compra y el regreso al agua mantiene el movimiento gradual.

Solo un diálogo final observado y posteriormente cerrado permite añadir la cantidad solicitada a la **estimación**. No es una verificación del pago ni de los cebos entregados. Si Comprar pudo enviarse y la secuencia quedó incompleta, no se acredita inventario ni se repite el pedido; después de cerrar el diálogo si es posible, se pide una corrección manual. Un pedido que falló antes de enviarse puede permitir volver a pescar, con una pausa antes de otra compra automática y el límite por sesión vigente.

## Parada, pruebas y límites

F10, F8 durante ejecución, pérdida de foco o esquina superior izquierda detienen las acciones y liberan entradas. Una vigilancia independiente intenta soltarlas si la interfaz deja de responder durante más de 500 ms. E tiene duración ajustable; los clics de compra duran aproximadamente 180 ms y los del número se separan 250 ms. No se acumulan acciones atrasadas en una ráfaga. Una liberación rechazada por Windows queda pendiente y bloquea nuevas pulsaciones.

Los botones de **Pruebas** envían entradas reales tras la cuenta atrás y pueden gastar Peli. Las pruebas de compra no necesitan cumplir umbral ni cronómetro y no continúan pescando. El modo `--self-test` es diferente: usa runtimes simulados y no envía entradas al juego. Los resultados específicos de una compilación deben consultarse en `docs/VERIFICACION.txt`; las simulaciones no validan la recepción real en Roblox.

Estas comprobaciones dependen de Windows, del proceso activo y de la imagen capturada. No garantizan tasas de captura, evitar expulsiones por inactividad, conservar objetos ni reconocer cualquier contador o menú. No cubren un cierre forzado o una desconexión. Un antivirus sin detecciones no constituye una certificación ni garantía absoluta. El ejecutable no tiene firma comercial.

El código se ofrece sin licencia concedida; se conserva [NOTICE.md](NOTICE.md). Para comunicar un fallo, evita publicar credenciales o capturas con información personal. [English usage and limitations](README.en.md).
