# Privacidad de CertBox

Actualización: 17 de noviembre de 2026.

CertBox procesa certificados y claves localmente. La aplicación no envía los archivos, su contenido ni las contraseñas a internet. No requiere una cuenta ni incluye anuncios, telemetría, análisis de uso o consultas automáticas de actualizaciones.

Los archivos elegidos y las contraseñas introducidas se utilizan en la memoria para la inspección, la comprobación del par certificado/clave y la exportación. Los archivos exportados se guardan en los destinos elegidos por el usuario. CertBox no instala certificados en el sistema.

El único dato guardado en la configuración es el idioma, en %LOCALAPPDATA%\CertBox\settings.json. En Linux, el mismo dato se guarda en $XDG_CONFIG_HOME/CertBox/settings.json o ~/.config/CertBox/settings.json. Los certificados, las claves, las contraseñas y el historial de archivos no se guardan en ese archivo. Eliminarlo restablece la elección inicial del idioma.

El guardado utiliza archivos temporales en el directorio de destino y, cuando es necesario, copias de seguridad transitorias para restaurar las salidas tras un error. Estos archivos se eliminan cuando la operación termina normalmente. Una interrupción abrupta del proceso o del equipo puede dejar archivos transitorios en el destino. El runtime también puede extraer sus propias bibliotecas a una carpeta de caché de ejecución; esa extracción no contiene los certificados ni las claves cargados.

Al pulsar los enlaces del proyecto, GitHub se abre en el navegador predeterminado, sujeto a la política de privacidad de ese sitio. CertBox no envía tus certificados, claves ni contraseñas a GitHub. Usa archivos ficticios para informar de problemas y mantén los datos reales en privado.

El código, la interfaz y el icono se produjeron con uso de IA durante el desarrollo. CertBox no utiliza servicios de IA durante su funcionamiento ni envía datos de certificados a esos servicios. Las bibliotecas de terceros conservan su autoría y sus licencias.

Para dudas o solicitudes, utiliza la página de soporte del repositorio oficial indicada en la sección Acerca de de la aplicación y en la página de la versión distribuida.
