# Transparencia y seguridad

El objetivo de SomeFishing GPO es que se pueda revisar qué hace el programa y compilarlo desde el código publicado. No se promete que ningún programa esté libre de todos los defectos o de cualquier riesgo.

## Qué hace

- Lee los píxeles de las zonas seleccionadas: minijuego, contador opcional y diálogo de compra opcional.
- Identifica la ventana de Roblox en primer plano y comprueba los límites del área seleccionada.
- Registra F6, F8 y F10 como atajos; consulta F10 para la protección de parada. No registra lo que escribes.
- Envía movimientos del ratón al punto de lanzamiento y clics normales mediante las funciones de Windows.
- Si habilitas los saltos durante la espera, envía pulsaciones breves de Espacio. No envía teclas de dirección ni verifica que el personaje haya saltado.
- Si habilitas compras, pulsa E, hace clic dentro de la zona de compra, utiliza Ctrl+A y Retroceso para reemplazar la cantidad y escribe únicamente dígitos. Esto puede gastar Peli del juego. Hay un límite configurable de intentos por sesión; la compra empieza desactivada.
- Guarda `ajustes.xml`, un informe `ultima-parada.txt` y el último registro de diagnóstico `ultima-prueba.txt` junto al ejecutable. No los sube a ningún servicio.
- Procesa las capturas del juego en memoria; no las guarda durante el uso normal.
- El botón Copiar resultado escribe el registro de la prueba en el portapapeles solo al pulsarlo. No lee el contenido previo del portapapeles ni lo envía a ningún servicio.

## Qué contiene la aplicación

La aplicación no tiene funciones de red, descargas, actualizaciones automáticas, lectura de credenciales, suscripciones, ejecución de comandos externos, inicio automático ni cambios en el antivirus. No lee la memoria del juego ni inyecta código. No requiere privilegios de administrador.

El lector de cebo usa la API de OCR local de Windows sobre imágenes en memoria. No transmite capturas a un servicio externo. Permite elegir un idioma OCR instalado o usar las preferencias de Windows; no instala idiomas. Un idioma elegido que no esté disponible produce un error de lectura, sin cambiar silenciosamente de motor. Una tarea separada procesa como máximo una captura pendiente por lector; no envía entradas al juego. Al cerrar el lector se descarta cualquier resultado pendiente.

Si la lectura original no coincide entre escalas, puede intentar una imagen que conserva únicamente los píxeles amarillos o anaranjados del contador y normaliza el tamaño de las letras. Exige dos lecturas iguales en esa imagen y que no contradigan ningún número reconocido en la original. Este procesamiento no sustituye letras por dígitos ni transforma una imagen blanca o vacía en cero.

El respaldo visual de x2 guarda solo dos máscaras binarias de 32 × 24 muestras de ese texto, sin la captura completa. Solo puede aportar el positivo 2; no inventa ceros ni evita la confirmación temporal del contador. Una cantidad OCR contradictoria bloquea ese respaldo.

El lector de compra usa el mismo OCR local en una tarea separada. El código incluye referencias binarias diminutas de las letras de los botones Sí/No, sin capturas del jugador ni su inventario. Los campos de un solo dígito se repiten visualmente en memoria para ayudar al OCR: exige resultados coincidentes entre las copias y las dos escalas, sin convertir letras en números. No escribe textos libres ni comandos.

El script de compilación ejecuta el compilador local de .NET Framework y utiliza los metadatos del Windows SDK instalado. El modo `--self-test` crea imágenes sintéticas, renders de la interfaz e informes en la carpeta indicada, sin enviar clics ni teclas al juego.

Los botones de la sección **Pruebas** son acciones reales, distintas del modo automatizado `--self-test`: pulsarlos autoriza una prueba concreta tras tres segundos de cuenta atrás. La simulación de cero sigue las opciones de compra o salto configuradas; la prueba de compra puede gastar Peli aunque la compra automática esté apagada. La prueba de compra utiliza la cantidad elegida, hasta el MAX, en un intento; la simulación de cero conserva una unidad. Ambas no guardan sus opciones temporales y terminan sin lanzar la caña. Las vistas de lectura siguen sin enviar entradas.

El registro local de la última prueba contiene estados, cantidades resumidas y errores; no incluye capturas. Puede incluir fragmentos breves del OCR de la zona del contador para explicar un fallo de lectura. Está limitado a unos 32 000 caracteres y se reemplaza al iniciar otra prueba. La zona de pesca puede omitirse al probar; en ese caso se debe comprobar manualmente que el minijuego esté cerrado. Las zonas que sí están configuradas mantienen la validación de límites y foco.

## Cronómetro, OCR y entradas

Cronómetro usa el reloj transcurrido de la aplicación, sin tareas de Windows ni servicios externos. No consulta el contador ni exige su zona. Compra entre rondas, respeta el límite por sesión y empieza un intervalo completo tras cada compra. Al detenerse cancela toda acción futura; reiniciar empieza una cuenta nueva.

Conserva la verificación de los menús, la cantidad y el MAX. Tras el botón final exige capturas nuevas sin diálogo reconocido, pero no confirma un aumento del inventario. Errores del lector no confirman cierre. No reintenta una compra fallida. Contador OCR sigue exigiendo cebo positivo después del cierre.

El OCR del contador añade contraste suave y exige x, × o * delante de la cantidad para rechazar lecturas parciales. Los números contradictorios quedan desconocidos; no se garantiza reconocer cualquier tamaño o cantidad. Contador OCR puede reponer al confirmar una cantidad igual o inferior al umbral configurado (inicialmente 2), cuando termina la pesca pendiente. Tras comprar, solo rearma el disparador al confirmar un valor superior al umbral, para evitar compras seguidas por una lectura baja persistente.

E y las teclas de compra se envían con códigos físicos. El puntero se mueve mediante SendInput; la pausa de menús y apuntado es configurable entre 200 y 3000 ms, inicialmente 700 ms. Exige otra lectura estable antes de pulsar 180 ms. En el número central hace dos pulsaciones separadas por 250 ms y espera antes de reemplazarlo. Cada pulsación de compra incorpora la posición absoluta de su destino, sin reutilizar la posición anterior de Sí. La liberación del botón no mueve el puntero, incluso al perder el foco. Si el cursor no queda sobre el botón se cancela el clic. No se usa el portapapeles para escribir cantidades. Sí recibe un único clic, sin reintentos; tampoco se reintentan E ni la confirmación final de compra. Un diálogo final inesperado antes de verificar la cantidad detiene el proceso sin solicitar otro pedido.

## Límites de protección

F10, F8 durante la ejecución, perder el foco de Roblox o llevar el ratón a la esquina superior izquierda detiene la macro y libera tanto el clic como Espacio. Una comprobación independiente intenta soltar las entradas si la interfaz no responde durante más de 500 ms; también limita cada pulsación de Espacio a unos 100–150 ms. Si Windows rechaza liberar una entrada, el programa conserva el intento pendiente y reintenta sin autorizar otra pulsación. Estas protecciones requieren que Windows y el proceso sigan funcionando; no cubren un cierre forzado del proceso.

La misma protección cubre E, Ctrl, A, Retroceso y los dígitos, e interrumpe el doble clic si se detiene la sesión entre sus pulsaciones. E se puede mantener entre 100 y 3000 ms (1000 por defecto) con revisiones del controlador; la protección independiente sigue soltándola si la interfaz deja de responder durante más de 500 ms. Las demás teclas de compra conservan sus pulsaciones breves. Los clics de compra se limitan a la zona elegida, mientras Roblox conserva el foco. Antes de Comprar se comprueban el menú, el MAX y la cantidad escrita. Se pulsa como máximo una vez el botón que confirma el pedido en cada intento. Los fallos de un diálogo detienen la sesión en lugar de repetir compras con resultado incierto.

La lectura de cebo requiere coincidencia entre dos escalas y confirmación en varias capturas. La ausencia de lectura no equivale a cero. Si un contador antes confirmado pierde el texto amarillo durante 8 segundos y varias capturas nuevas, se informa como desaparecido y puede activar la reposición o la espera. Una ventana u objeto que lo tape puede parecer una desaparición. Estas comprobaciones reducen lecturas erróneas, pero no garantizan la exactitud del OCR. Tres lanzamientos sin minijuego pueden activar la espera. En Contador OCR no autorizan compras por sí solos; en Cronómetro la autorización es el intervalo configurado. El cierre normal de una ronda no basta para activar ninguna de esas acciones. No se garantiza evitar expulsiones por inactividad ni conservar objetos ante una desconexión.

Los análisis locales de Microsoft Defender y sus resultados se documentan en `docs/VERIFICACION.txt`. Un resultado sin amenazas detectadas no es una garantía absoluta ni una certificación externa. El ejecutable no tiene firma digital comercial. La huella SHA-256 de cada publicación identifica exactamente el archivo analizado.

## Informar de problemas

Puedes abrir un issue para fallos de detección o funcionamiento. Evita publicar credenciales, capturas con datos personales o información privada. Si el problema implica datos sensibles, contacta primero con el titular del repositorio para acordar un canal privado.
