# Cambios

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
