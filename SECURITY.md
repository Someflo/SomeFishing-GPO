# Transparencia y seguridad

El objetivo de SomeFishing GPO es que se pueda revisar qué hace el programa y compilarlo desde el código publicado. No se promete que ningún programa esté libre de todos los defectos o de cualquier riesgo.

## Qué hace

- Lee los píxeles de la zona elegida por la persona que lo utiliza.
- Identifica la ventana de Roblox en primer plano y comprueba los límites del área seleccionada.
- Registra F6, F8 y F10 como atajos; consulta F10 para la protección de parada. No registra lo que escribes.
- Envía movimientos del ratón al punto de lanzamiento y clics normales mediante las funciones de Windows.
- Guarda `ajustes.xml` y un único informe `ultima-parada.txt` junto al ejecutable.
- Procesa las capturas del juego en memoria; no las guarda durante el uso normal.

## Qué contiene la aplicación

La aplicación no tiene funciones de red, descargas, actualizaciones automáticas, lectura de credenciales, suscripciones, ejecución de comandos externos, inicio automático ni cambios en el antivirus. No lee la memoria del juego ni inyecta código. No requiere privilegios de administrador.

El script de compilación ejecuta el compilador local de .NET Framework. El modo `--self-test` crea imágenes sintéticas, renders de la interfaz e informes en la carpeta indicada, sin enviar clics al juego.

## Límites de protección

F10, perder el foco de Roblox o llevar el ratón a la esquina superior izquierda detiene la macro. Una comprobación independiente intenta soltar el clic si la interfaz no responde durante más de 500 ms. Si Windows rechaza soltarlo, se mantiene el intento pendiente y se reintenta. Estas protecciones requieren que Windows y el proceso sigan funcionando; no cubren un cierre forzado del proceso.

Los análisis locales de Microsoft Defender y sus resultados se documentan en `docs/VERIFICACION.txt`. Un resultado sin amenazas detectadas no es una garantía absoluta ni una certificación externa. El ejecutable no tiene firma digital comercial. La huella SHA-256 de cada publicación identifica exactamente el archivo analizado.

## Informar de problemas

Puedes abrir un issue para fallos de detección o funcionamiento. Evita publicar credenciales, capturas con datos personales o información privada. Si el problema implica datos sensibles, contacta primero con el titular del repositorio para acordar un canal privado.

