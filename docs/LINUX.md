# CertBox no Linux

**A versão Linux não foi testada manualmente.** Compilação e verificações
automatizadas não substituem testes reais em diferentes distribuições e ambientes
gráficos. Consulte as notas da release para o alcance exato das verificações feitas.

As verificações automatizadas do runtime reduzido passaram em Debian 12 via WSL,
sem exibir janelas: leitura e exportação de certificados/chaves, construção dos
controles, idiomas, preferências e documentos incorporados. Arraste real,
diálogos nativos e o uso em outros desktops continuam sem conferência manual.

Desenvolvido com IA. Interface, código e ícone produzidos com uso de IA;
bibliotecas externas mantêm sua autoria e suas licenças.

## Uso

Baixe certbox-linux-x64.tar.gz e extraia pelo gerenciador de arquivos. Abra
CertBox/certbox. O pacote preserva a permissão de execução e contém uma aplicação
gráfica, sem versão CLI nem iniciador shell/bash. Alguns gerenciadores exigem
autorizar a execução nas propriedades do arquivo ou escolher “Executar”.

O runtime .NET está incluído. A compilação usa Linux x64 com glibc, não musl;
Alpine e ARM não são alvos desta entrega. Requer ambiente gráfico X11 ou XWayland,
fontconfig, FreeType e as bibliotecas comuns de um desktop Linux. Criptografia
usa OpenSSL do sistema, normalmente já presente na distribuição.

Em Debian/Ubuntu, os componentes relevantes incluem libc6, libgcc-s1,
libstdc++6, zlib1g, libssl3 (ou o equivalente da distribuição), libx11-6,
libxext6, libice6, libsm6, libxrandr2, libxinerama1, libxi6, libxcursor1,
libxrender1, libxfixes3 e libfontconfig1.
Seletores de arquivos podem usar o portal do desktop via D-Bus ou GTK 3 como
alternativa. Os nomes dos pacotes variam por distribuição. A versão Avalonia
11.3.23 utiliza X11; em sessões Wayland depende de XWayland.

O idioma fica em $XDG_CONFIG_HOME/CertBox/settings.json ou, quando essa variável
não define um caminho absoluto, ~/.config/CertBox/settings.json. A sugestão
inicial considera LC_ALL, LC_MESSAGES, LANGUAGE e LANG. Somente o idioma é salvo.

O ícone é fornecido no pacote e usado na janela. A extração não instala atalhos
ou arquivos no sistema automaticamente. Projeto e suporte:
https://github.com/gabrielx8v/CertBox.

## English

**The Linux version has not been manually tested.** Extract the Linux x64 archive
and open CertBox/certbox in a file manager. This is a graphical application;
there is no separate CLI version or shell launcher. The .NET runtime is bundled,
but a glibc-based Linux desktop with X11/XWayland, fontconfig, FreeType, system
OpenSSL and the usual desktop libraries is required. File dialogs can use a
D-Bus desktop portal or GTK 3. Alpine/musl and ARM are not targets of this build.
Language settings use $XDG_CONFIG_HOME/CertBox or ~/.config/CertBox.
Developed with AI; third-party components retain their authorship and licenses.

## Español

**La versión Linux no ha sido probada manualmente.** Extrae el archivo Linux x64
y abre CertBox/certbox desde el gestor de archivos. Es una aplicación gráfica,
sin versión CLI ni iniciador shell. Incluye el runtime .NET, pero requiere un
escritorio Linux basado en glibc con X11/XWayland, fontconfig, FreeType, OpenSSL
del sistema y las bibliotecas habituales del escritorio. Los selectores de
archivos pueden usar el portal D-Bus o GTK 3. Alpine/musl y ARM no son objetivos
de esta compilación. El idioma se guarda en $XDG_CONFIG_HOME/CertBox o
~/.config/CertBox. Desarrollado con IA; los componentes externos conservan sus
autorías y licencias.
