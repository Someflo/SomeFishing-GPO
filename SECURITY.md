# Transparencia y seguridad

El objetivo de SomeFishing GPO es que se pueda revisar qué hace el programa y compilarlo desde el código publicado. No se promete que ningún programa esté libre de todos los defectos o de cualquier riesgo.

## Qué hace

- Lee los píxeles de las zonas seleccionadas: minijuego, contador opcional y diálogo de compra opcional.
- Identifica la ventana de Roblox en primer plano y comprueba los límites del área seleccionada.
- Registra F6, F8 y F10 como atajos; consulta F10 para la protección de parada. No registra lo que escribes.
- Envía movimientos del ratón al punto de lanzamiento y clics normales mediante las funciones de Windows.
- Si habilitas los saltos durante la espera, envía pulsaciones breves de Espacio. No envía teclas de dirección ni verifica que el personaje haya saltado.
- Si habilitas compras, pulsa E, hace clic dentro de la zona de compra, utiliza Ctrl+A y Retroceso para reemplazar la cantidad y escribe únicamente dígitos. Esto puede gastar Peli del juego. Hay un límite configurable de intentos por sesión; la compra empieza desactivada.
- Guarda `ajustes.xml` y un único informe `ultima-parada.txt` junto al ejecutable.
- Procesa las capturas del juego en memoria; no las guarda durante el uso normal.

## Qué contiene la aplicación

La aplicación no tiene funciones de red, descargas, actualizaciones automáticas, lectura de credenciales, suscripciones, ejecución de comandos externos, inicio automático ni cambios en el antivirus. No lee la memoria del juego ni inyecta código. No requiere privilegios de administrador.

El lector de cebo usa la API de OCR local de Windows sobre imágenes en memoria. No transmite capturas a un servicio externo. Necesita un idioma OCR disponible en el perfil de Windows y no lo instala automáticamente. Una tarea separada procesa como máximo una captura pendiente por lector; no envía entradas al juego. Al cerrar el lector se descarta cualquier resultado pendiente.

El lector de compra usa el mismo OCR local en una tarea separada. El código incluye referencias binarias diminutas de las letras de los botones Sí/No, sin capturas del jugador ni su inventario. Los campos de un solo dígito se repiten visualmente en memoria para ayudar al OCR: exige resultados coincidentes entre las copias y las dos escalas, sin convertir letras en números. No escribe textos libres ni comandos.

El script de compilación ejecuta el compilador local de .NET Framework y utiliza los metadatos del Windows SDK instalado. El modo `--self-test` crea imágenes sintéticas, renders de la interfaz e informes en la carpeta indicada, sin enviar clics ni teclas al juego.

## Límites de protección

F10, F8 durante la ejecución, perder el foco de Roblox o llevar el ratón a la esquina superior izquierda detiene la macro y libera tanto el clic como Espacio. Una comprobación independiente intenta soltar las entradas si la interfaz no responde durante más de 500 ms; también limita cada pulsación de Espacio a unos 100–150 ms. Si Windows rechaza liberar una entrada, el programa conserva el intento pendiente y reintenta sin autorizar otra pulsación. Estas protecciones requieren que Windows y el proceso sigan funcionando; no cubren un cierre forzado del proceso.

La misma protección cubre E, Ctrl, A, Retroceso y los dígitos. Los clics de compra se limitan a la zona elegida, mientras Roblox conserva el foco. Antes de Comprar se comprueban el menú, el MAX y la cantidad escrita. Se envía como máximo un clic de compra por intento. Los fallos de un diálogo detienen la sesión en lugar de repetir compras con resultado incierto.

La lectura de cebo requiere coincidencia entre dos escalas y confirmación en varias capturas. La ausencia de lectura no equivale a cero. Si un contador antes confirmado pierde el texto amarillo durante 8 segundos y varias capturas nuevas, se informa como desaparecido y puede activar la reposición o la espera. Una ventana u objeto que lo tape puede parecer una desaparición. Estas comprobaciones reducen lecturas erróneas, pero no garantizan la exactitud del OCR. Tres lanzamientos sin minijuego pueden activar la espera, pero no autorizan compras. El cierre normal de una ronda no basta para activar ninguna de esas acciones. No se garantiza evitar expulsiones por inactividad ni conservar objetos ante una desconexión.

Los análisis locales de Microsoft Defender y sus resultados se documentan en `docs/VERIFICACION.txt`. Un resultado sin amenazas detectadas no es una garantía absoluta ni una certificación externa. El ejecutable no tiene firma digital comercial. La huella SHA-256 de cada publicación identifica exactamente el archivo analizado.

## Informar de problemas

Puedes abrir un issue para fallos de detección o funcionamiento. Evita publicar credenciales, capturas con datos personales o información privada. Si el problema implica datos sensibles, contacta primero con el titular del repositorio para acordar un canal privado.
