# Seguridad y transparencia

SomeFishing GPO funciona localmente: captura las zonas elegidas, analiza píxeles y envía entradas normales de Windows. No lee ni modifica la memoria de Roblox, inyecta código, descarga archivos, recoge credenciales, cambia el antivirus ni requiere administrador. No tiene funciones de red, inicio automático ni actualizador.

## Entradas y datos

El seguimiento usa clic sostenido y un punto de lanzamiento elegido por el usuario. Los saltos opcionales usan Espacio, sin direcciones. Las compras usan E, cinco clics en los puntos marcados, Ctrl+A, Retroceso y dígitos. Pueden gastar Peli del juego. No leen el portapapeles ni escriben textos libres.

F6/F8/F10 controlan la selección, inicio y parada. No se registra lo escrito en otras aplicaciones. Antes de cada entrada se comprueba el foco de Roblox y que los puntos estén dentro de su ventana. La ventana bajo el cursor también debe corresponder a Roblox. Se cancela un clic si el cursor se apartó del destino.

Los ajustes y registros se guardan junto al ejecutable: `ajustes.xml`, `ultima-parada.txt`, `ultima-prueba.txt`. Las capturas de uso normal se procesan en memoria. Los registros pueden contener cantidades, coordenadas, fragmentos breves de OCR, nombres de proceso e identificadores de ventana; no títulos, rutas ni capturas. Copiar resultado escribe el registro al portapapeles solo al pulsarlo.

## Compra por puntos

La interfaz 0.6.0 usa tres puntos marcados expresamente. El izquierdo corresponde a Sí/Comprar; el central, al número y al cierre; el derecho se valida, sin pulsarlo. No deriva destinos de un rectángulo ni exige OCR de los menús. Los diálogos deben mantener esos destinos y estar cerrados antes de iniciar.

Contador OCR compra la diferencia entre capacidad configurada y cantidad confirmada al alcanzar el umbral. Cronómetro compra la cantidad fija al vencer el intervalo. Pausa el seguimiento, libera el clic y espera el cierre del minijuego antes de E. Si había un lanzamiento pendiente, respeta su espera de picada. La pesca continúa al terminar la secuencia.

La secuencia solo envía un pedido por intento. No comprueba aceptación del número, saldo, MAX del menú, pago ni cierre. El resultado se informa como enviado y sin verificar. Una prueba manual con una unidad permite comprobar los puntos antes de habilitar compras repetidas. El límite por sesión acota los intentos. Un fallo de entrada, foco o tiempo detiene la secuencia; no reintenta automáticamente. El disparador por contador se rearma solo tras confirmar cebo por encima del umbral.

## OCR y protecciones

El contador usa OCR local de Windows y referencias visuales pequeñas del texto x2/x3/x4. Las referencias son máscaras binarias de letras, sin imagen del jugador o inventario. Requiere coherencia espacial y confirmación temporal; una lectura OCR contradictoria impide aceptar el respaldo visual. No interpreta una captura vacía como cero. La compra por cronómetro no consulta el contador. El código y pruebas del lector de menús anterior se conservan para revisión y regresiones; la interfaz no lo usa al comprar.

F10, F8 durante ejecución, pérdida de foco o esquina superior izquierda detienen las acciones y liberan las entradas. Una vigilancia independiente intenta soltarlas si la interfaz deja de responder durante más de 500 ms. E tiene duración ajustable; los clics de compra duran unos 180 ms y el doble clic del número separa las pulsaciones por 250 ms. No se acumulan acciones atrasadas en un solo ciclo. Si Windows rechaza una liberación, queda pendiente y bloquea nuevas pulsaciones.

Estas comprobaciones dependen de Windows y del proceso activo. No garantizan una tasa de capturas, evitar expulsiones por inactividad, conservar objetos o reconocer cualquier contador. Tampoco cubren un cierre forzado o una desconexión.

Los botones de Pruebas envían entradas reales con una cuenta atrás de tres segundos y pueden gastar Peli. El modo `--self-test` es distinto: usa remitentes simulados, sin entradas al juego. Los resultados y el análisis local de Microsoft Defender se documentan en `docs/VERIFICACION.txt`; un resultado sin detecciones no es una certificación ni garantía absoluta. El ejecutable no tiene firma comercial.

El código se ofrece sin licencia concedida; se conserva `NOTICE.md`. Para comunicar un fallo, evita publicar credenciales o capturas con información personal.