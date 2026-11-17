# Qué comprueba CertBox

CertBox inspecciona certificados X.509 y exporta certificados y claves en archivos CRT, PEM y KEY. El procesamiento es local y las operaciones de exportación protegen los archivos de entrada contra sobrescritura.

El indicador de plazo compara las fechas del certificado con el reloj del equipo. No confirma la confianza pública, la correspondencia con un sitio web, la revocación ni la validez completa para TLS. La ordenación de la cadena utiliza solo los certificados proporcionados; la aplicación no consulta ni descarga emisores por internet.

Las casillas controlan el contenido exportado. El selector del panel de datos cambia solo el certificado visualizado. Todas las casillas comienzan sin marcar.

Exportar CRT guarda solo los certificados marcados, sin clave privada. Crear PEM también incluye la clave cargada si el certificado del dominio está marcado, tras comprobar el par. Guardar clave escribe solo la clave. Se conserva el cifrado de las claves protegidas; crear un PEM con una clave protegida requiere la contraseña para comprobar el par.

Se aceptan certificados PEM/DER y claves RSA, EC y DSA en los formatos documentados en el README. No se admiten PFX/P12, PKCS#7, OpenSSH, Ed25519 ni PEM con cifrado antiguo Proc-Type. Los límites son 32 archivos de certificados, una clave, 128 certificados y 16 MB por archivo.

Los archivos CRT y KEY también pueden contener texto PEM; la extensión indica su finalidad habitual y no garantiza la codificación. La aplicación identifica el contenido del archivo.
