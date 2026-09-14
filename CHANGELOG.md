# Historial de cambios

## 0.8.0 · Inventario, tipos de cebo y recuperación

- Nuevo apartado **Cebos**: cantidades manuales de legendario, raro y común, con confirmación explícita antes de iniciar pesca. Conserva las estimaciones en los ajustes y permite corregirlas.
- Usa los tipos en orden legendario → raro → común. Selecciona el menú completo, localiza las filas de nuevo al cambiar su disposición y comprueba el borde amarillo del tipo seleccionado.
- Cuenta rondas iniciadas, rondas terminadas y lanzamientos por separado. Descuenta una unidad estimada por minijuego detectado, sin repetir el descuento entre imágenes. Un lanzamiento sin minijuego no descuenta. Las rondas terminadas incluyen capturas y escapes.
- **Inventario y rondas** reemplaza al OCR como disparador principal: repone común hasta la capacidad al alcanzar el umbral. Conserva el cronómetro ajustable de 50 comunes cada 40 minutos; la reposición espera el uso de común y puede adelantarse si se agota el inventario.
- OCR opcional como contraste de la fila activa. No sustituye las cantidades manuales ni interpreta un menú desconocido como cero.
- Recorte automático del contador de la fila activa, separando el borde amarillo de los caracteres antes del OCR. La mejora del recorte no equivale a confirmar la lectura numérica de Windows.
- Detección de pesca apoyada en los bordes reales de la barra azul, sin exigir que cubra el 60 % de la altura de la zona. Limita la búsqueda de pez y hueco a su interior e ignora verde y texto externo.
- Conserva el control durante pérdidas visuales de hasta 180 ms y después libera el clic. Una interrupción breve no crea otra ronda ni otro descuento de cebo; no devuelve posiciones antiguas como si fueran una detección nueva.
- Reintentos ajustables por falta de minijuego y por fase de compra, con tiempo máximo. Identifica Sí/No, cantidad, cierre con tres puntos y ausencia mediante capturas nuevas. Rechaza evidencia vieja o repetida y vuelve a comprobar acciones atrasadas.
- Recuperación de compra: cancela con el punto derecho o cierra los tres puntos según el menú reconocido. Solo retoma pesca cuando observa el cierre; un menú ambiguo o persistente termina la recuperación.
- Comprar se marca enviado antes de llamar al controlador de entrada para impedir un segundo pedido ante un error incierto. Una compra incompleta no acredita cebos ni se vuelve a enviar; pide corregir el inventario. Un diálogo terminado solo acredita una estimación de la cantidad solicitada, sin afirmar el pago o entrega reales.
- Mantiene Español/English y amplía las traducciones, guía y pruebas simuladas para inventario, menús, reintentos y selección. La validación en el juego sigue siendo distinta de las pruebas sin entradas reales.

## 0.7.1 · Español e inglés

- Selector **Idioma / Language** visible en la barra lateral de todos los apartados: Español y English. Cambia la interfaz al instante y guarda la preferencia.
- Traduce navegación, controles, ayudas, selección de zonas, mensajes de estado, errores propios, guía y diagnósticos. El idioma OCR continúa siendo una configuración independiente.
- Conserva áreas, coordenadas, cantidades, temporizador, idioma OCR y permisos al cambiar de idioma, sin reconstruir la ventana ni alterar la lógica de pesca o compra.
- Traducciones incluidas en el ejecutable, sin descargas. Los ajustes anteriores comienzan en español si todavía no incluyen un idioma de interfaz.
- Añade una guía de inicio en inglés y comprobaciones de persistencia, textos dinámicos, cambio de idioma y conservación de configuraciones. Los diálogos y errores de Windows pueden mantener el idioma del sistema; los registros históricos conservan el idioma en que se guardaron.

## 0.7.0 · Regreso gradual al agua e interfaz sencilla

- Todos los lanzamientos, incluido el regreso de una compra, mueven el puntero gradualmente al agua y esperan llegada, estabilidad y 200 ms antes de pulsar. La duración del clic empieza al presionar.
- Cancela el lanzamiento pendiente si aparece un minijuego, se detiene la sesión, cambia el foco o toca una compra antes de lanzar.
- Menú con Inicio, Zonas, Pruebas y Avanzado. OCR o cronómetro y los valores sencillos quedan en Inicio; áreas y botones se marcan en Zonas.
- Los ajustes técnicos pasan a Avanzado con el aviso «Si no conoces estos ajustes, déjalos como están». Se conservan valores, puntos y preferencias anteriores.
- La secuencia de compra de 0.6.1, cuyo funcionamiento confirmó el usuario, se conserva. Las pruebas de compra siguen terminando sin lanzar la caña.

## 0.6.1 · Sincronización de los clics de compra

- Sustituye el movimiento absoluto de compra por desplazamientos relativos con corrección de posición, estabilidad y pausa antes de pulsar.
- Unifica los clics: Sí, número, Comprar y cierre ya no mezclan coordenadas nuevas con el evento de pulsación.
- Verifica los colores de los tres botones en dos imágenes nuevas después de Sí y antes del doble clic, la escritura y Comprar; detiene el paso si no aparece el menú de cantidad. Sin OCR ni nuevas zonas.
- Registra las teclas enviadas, además del ratón y los colores observados. Conserva puntos, ajustes y los dos modos de reposición.
- Incluye regresiones del Sí fallido, menú que desaparece durante el movimiento, imágenes repetidas y cancelación. La recepción real de los clics y la cantidad escrita requieren comprobación en el juego.
## 0.6.0 · Compra simplificada y aviso de cebo bajo

- Tres puntos marcados por el usuario: Sí/Comprar, número/…, No/Cancelar. No se calculan posiciones a partir de una zona ni se leen los menús para avanzar.
- Dos modos: contador confirmado en 2 o menos completa la capacidad configurada (300 inicialmente); cronómetro solicita 50 cada 40 minutos. Valores ajustables.
- Al tocar la compra, pausa el seguimiento y libera el clic. Espera cierre del minijuego o la picada pendiente; envía una secuencia con pausas y vuelve a pescar.
- Probar compra usa una cantidad independiente y un solo intento, sin esperar contador, umbral ni cronómetro. Conserva foco, puntos, permiso de entradas y F10.
- Añade referencias visuales de x3 y x4 y aviso de compra próxima. El disparador sigue en 2 o menos; no convierte lecturas desconocidas o desapariciones en cero.
- Resultado de la secuencia explícitamente sin verificar en el juego. Sin reintentos dentro de un pedido; límite por sesión y rearme del contador solo por encima del umbral.
- Interfaz de Compra y Pruebas más breve; conserva ajustes existentes y requiere marcar los tres puntos nuevos.
## 0.5.4 — lectura de MAX y continuación manual (compilación local)

- Corrige tres fallos reproducidos en la nueva captura: Comprar cortado por la división del área, MAX 294 leído como dígitos truncados y localización del botón verde con un respaldo que solo buscaba blanco.
- Amplía de forma acotada la región de Comprar, localiza sus letras verdes tras reconocerlas y lee la línea completa de MAX ensanchada a varias escalas. Requiere acuerdo de dos escalas sin números contradictorios; no convierte letras o comillas en dígitos.
- La imagen completa y el recorte 519 × 199 se reconocen como MAX 294 y cantidad 1 con Automático y español. Inglés permanece desconocido en esa captura.
- Puede continuar si Sí se pulsó manualmente durante una sesión activa, tras dos lecturas nuevas válidas. No repite Sí ni reanuda sesiones detenidas por pérdida de foco.
- Comprueba la ventana bajo el cursor antes de pulsar. El registro distingue pulsación y liberación aceptadas por Windows, duración y ventana que tomó el foco. Esa aceptación no demuestra que Roblox respondiera.
- 390 comprobaciones integradas y 477 con doce capturas privadas. Conserva ajustes y los métodos de clic de la 0.5.3; el clic inicial dentro de Roblox todavía requiere comprobarse con el nuevo diagnóstico.

## 0.5.3 — restaurar el clic inicial de Sí (compilación local)

- Restaura el movimiento separado y la pulsación clásica de la 0.5.1 para Sí, Comprar y «…», tras el fallo del clic inicial comunicado con la 0.5.2.
- Limita el evento con coordenadas a las dos pulsaciones del número central. El controlador indica explícitamente el tipo de clic; el registro muestra cuál se envió.
- Mantiene un único Sí, las pausas, el doble clic solo en la cantidad, la comprobación de MAX y las paradas de protección. Conserva todos los ajustes existentes.
- 378 comprobaciones integradas y 454 con once capturas privadas. Se verifica la secuencia completa de tipos de clic y el formato clásico de Sí. El resultado dentro de Roblox sigue pendiente de comprobar.

## 0.5.2 — destino del doble clic (compilación local)

- Cada pulsación de compra incluye la coordenada de destino en el propio evento del ratón, además del movimiento previo. La liberación de protección conserva su comportamiento sin mover el puntero.
- Sí recibe un único clic, sin reintentos. Las dos pulsaciones de cantidad conservan una posición propia, distinta del botón de compra; una lectura que superponga ambos destinos detiene el proceso.
- El registro identifica tanto la posición del número como la de Comprar. Si aparece el diálogo final antes de confirmar la cantidad, informa del cambio inesperado y se detiene.
- 371 comprobaciones integradas y 447 con once capturas privadas. Incluye las coordenadas del fallo informado, separación de destinos y eventos con posición en escritorios múltiples. Falta validar la recepción de los clics dentro de Roblox.

## 0.5.1 — flujo de compra y umbral OCR (compilación local)

- Doble clic en el número central antes de seleccionar y reemplazar la cantidad; pausa de menús y apuntado ajustable, inicialmente 700 ms.
- Posiciones de Sí, cantidad y Comprar tomadas del texto reconocido. Admite Sí como confirmación del menú de cantidad, tras reconocer MAX. Un solo reintento del primer Sí si persiste la oferta en capturas nuevas; no repite E ni Comprar.
- Reconoce los tres puntos de la captura real entre etiquetas del HUD y pulsa su posición visible. El mismo fondo sin los puntos no autoriza el cierre.
- Comprar cuando queden 2 cebos o menos, ajustable y compatible con MAX. Espera a terminar la pesca y rearma tras confirmar cebo por encima del umbral.
- Selector de idiomas OCR instalados para contador y compra, con opción automática. No descarga idiomas ni oculta una selección no disponible.
- Registro con posiciones de clic y pausas; conserva cronómetro de 50 cada 40 minutos y cantidad de prueba independiente.
- 361 comprobaciones integradas en el equipo de desarrollo y 437 con once capturas privadas. Incluye cancelación durante el doble clic y persistencia de ajustes. El flujo completo sigue pendiente de validación dentro del juego.

## 0.5.0 — cronómetro y entradas (compilación local)

- Compra por contador o cronómetro; cantidad e intervalo ajustables, inicialmente 50 cada 40 minutos, hasta el MAX del menú.
- Cuenta atrás visible, compra entre rondas, nuevo intervalo tras completar el diálogo, límite por sesión y cancelación al parar. Espera al cronómetro tras tres lanzamientos fallidos, con saltos opcionales.
- Cantidad independiente en Pruebas, para una compra inmediata. La simulación de cero conserva una unidad.
- E y teclas de compra por códigos físicos; apuntar separado de pulsar, pausa mínima de 200 ms, otra confirmación visual y comprobación de la posición. Clic de 180 ms.
- Contraste suave del contador y prefijo obligatorio; evita una lectura parcial de x42 como 2. No resuelve todas las cantidades ni tamaños.
- Un error del lector no confirma el cierre de la compra. Cronómetro no verifica inventario mediante OCR.
- 332 comprobaciones integradas y 404 con capturas privadas. Sin entradas reales durante la validación ni cambio de licencia.


## 0.4.0 — interfaz más simple (compilación local)

- Menú lateral con Pesca, Cebo, Compra, Pruebas, Ajustes y Guía. Diseño claro con paneles blancos, fondo suave y botones redondeados, sin emojis.
- Inicio, parada y estado visibles en todas las secciones. Guardar en un único lugar y una guía breve.
- Botones y textos más cortos; los detalles aparecen al pasar el cursor. El resumen de la prueba sin cebo refleja si comprará, saltará o no hará ninguna acción.
- Copiar resultado facilita compartir el registro de una prueba, mediante una acción explícita del usuario.
- Presentación separada en `src/Interface.cs`. Ajustes, detección, control y flujo de compra conservados. 277 comprobaciones integradas y 347 con capturas de desarrollo; las seis vistas se revisaron visualmente.

## 0.3.3 — contador x2 y apertura de compra con E (compilación local)

- Respaldo visual del x2 aportado cuando el OCR no lo reconoce. Dos máscaras binarias pequeñas, margen obligatorio y comparación de la forma completa; solo aportan 2 y nunca reemplazan cantidades contradictorias ni confirman cero.
- Mantener E para abrir configurable de 100 a 3000 ms, con 1000 ms iniciales en lugar de 150. Se conserva la respuesta a F10, pérdida de foco y vigilancia de entradas. No hay reintentos automáticos de E tras un fallo.
- El registro muestra la duración de E. Los ajustes anteriores siguen siendo compatibles, sin perder zonas u opciones.
- 277 comprobaciones integradas y 347 con diez capturas privadas. La captura x2 y sus reconstrucciones se reconocen; se rechazan otras formas y recortes del x300 real. No se realizaron compras reales durante la validación.

## 0.3.2 — botones de prueba y diagnóstico de compra (compilación local)

- Nueva pestaña Pruebas: simular 0 cebos y probar una compra real de una unidad, con cuenta atrás de tres segundos.
- La simulación sigue la reacción configurada: una compra, un salto o aviso de funciones desactivadas. La prueba directa de compra funciona aunque haya cebo.
- Las pruebas terminan automáticamente sin lanzar la caña; limitan compras a 1 unidad y 1 intento, sin cambiar los ajustes habituales. Conservan F10/F8, pérdida de foco y liberación de entradas.
- Registro visible y local en `ultima-prueba.txt`, con estado, contador y si se envió Comprar. Los fallos identifican E, Sí, cantidad, botón final o contador sin confirmar. No repiten una compra incierta.
- La prueba permite omitir la zona de pesca y el punto de lanzamiento. Toda zona configurada mantiene su validación. La simulación de cero se descarta antes de aceptar lecturas reales después de la compra.
- 238 comprobaciones integradas; 296 con nueve capturas privadas. Entradas simuladas durante la validación: no se realizaron compras ni saltos reales en el juego.

## 0.3.1 — lectura del contador con franjas blancas (versión preliminar)

- Corrige el caso en el que la vista muestra `x300` pero el lector no confirma el contador al incluir una franja blanca o recibir letras de distinto tamaño.
- Como alternativa a la lectura original, aísla los píxeles amarillos/anaranjados y normaliza la altura del texto. Mantiene la confirmación entre dos escalas y rechaza cualquier número contradictorio.
- 205 comprobaciones integradas y 263 con nueve capturas privadas. La nueva vista ampliada y su reconstrucción a 44 × 25 píxeles reconocen 300. Franjas blancas sin letras y bordes finos no se convierten en cantidades.
- La captura nueva corresponde a la vista previa ampliada, no al recorte original de pantalla. Falta comprobar la selección real con la vista de lectura del usuario. No se enviaron entradas al juego durante las pruebas.

## 0.3.0 — reposición de cebo y contador desaparecido (compilación local)

- Detecta la desaparición prolongada del contador solo después de una lectura confirmada en la sesión: al menos cuatro capturas nuevas y ocho segundos sin texto amarillo. Conserva el estado desaparecido separado del cero.
- Compra automática opcional al confirmar cero o esa desaparición, con prioridad sobre los saltos. Termina primero cualquier pesca activa. Tres lanzamientos fallidos por sí solos no autorizan compras.
- Tercera zona para el diálogo y sus botones, con vista de prueba sin clics. Reconocimiento del texto, MAX, cantidad y botones Sí/No.
- Flujo E → Sí → reemplazar cantidad → comprobar número → Comprar una vez → cerrar «…» → confirmar cebo nuevo → volver a pescar.
- Cantidad fija o máximo del menú, respetando su límite. Tope inicial de diez intentos por sesión. La compra está desactivada por defecto.
- Liberación de las teclas de compra, F10/F8, pérdida de foco, límites de espera y bloqueo de reintentos tras un resultado incierto.
- 202 comprobaciones integradas y 258 con ocho capturas privadas. Reconoce la oferta y el menú MAX 5/cantidad 1; los contadores 300 y 295 se leen al excluir el borde amarillo.
- El recorte final no muestra «…»; esa detección se validó con ejemplos sintéticos. El contador queda oscurecido por ese diálogo, por lo que se comprueba después de cerrarlo. No se enviaron compras reales durante las pruebas.

## 0.2.0 — lectura de cebo y saltos en espera (compilación local)

- Pestaña Cebo y espera, con una segunda zona para el número del cebo equipado y una prueba de lectura que no envía entradas al juego.
- OCR local de Windows en segundo plano, sin descargas. Exige coincidencia entre dos escalas y varias capturas: dos para cantidades positivas y tres para confirmar cero. Las lecturas ausentes o ambiguas permanecen desconocidas.
- Espera con saltos opcionales al confirmar cero cebos o fallar tres lanzamientos sin minijuego. Intervalo de 60 segundos por defecto, ajustable entre 15 y 300 segundos. No salta tras el cierre normal de cada ronda.
- Termina la pesca activa y respeta la espera de la última picada antes de pasar al modo de espera por falta de cebo.
- Reanuda el seguimiento al reaparecer el minijuego y los lanzamientos al confirmar cebo repuesto después de una espera por cero.
- Pulsaciones breves de Espacio sin dirección, con liberación independiente y paradas por F10, F8, pérdida de foco o esquina superior izquierda.
- Informe de parada con cantidad confirmada de cebo y pulsaciones de salto solicitadas. Ambas funciones nuevas están desactivadas por defecto.
- 162 comprobaciones integradas y 213 con las capturas del desarrollo. En la captura del contador reconoce 300 comunes; 42 raros no se reconoce con fiabilidad y se informa como desconocido.
- La compilación requiere ahora el Windows SDK y .NET Framework 4.8. La ejecución del OCR necesita Windows 10/11 y un idioma OCR instalado; el ejecutable no necesita el SDK.

Las pruebas usan entradas simuladas. Falta validar los saltos y el contador durante una partida real. Esta versión todavía no se ha publicado en GitHub.

## 0.1.0 — primera publicación

- Detección visual de la barra azul, el hueco gris y la línea blanca del pez.
- Una zona configurable con margen para el pequeño balanceo lateral del menú.
- Exclusión de la barra verde de progreso y de marcas blancas externas a la barra localizada.
- Clic mantenido para subir; clic liberado para bajar. Frenado adicional al seguir un pez casi quieto.
- Lanzamiento, espera, minijuego y repetición al cerrarse el menú.
- Valores iniciales de lanzamiento: 220 ms; espera de picada: 15 s.
- F6 para seleccionar zona, F8 para iniciar o detener y F10 para detener.
- Vista del detector sin clics, confirmación visual de la selección y ajustes locales.
- Paradas por pérdida de foco o detección, límites de espera y protección independiente del clic.
- Informe local de la última parada.
- Código completo, compilación con herramientas de Windows y pruebas sin interacción con el juego.

Esta es la primera versión pública con el nombre SomeFishing GPO. Se basa en el prototipo local 1.4; la numeración pública comienza en 0.1.0.
