# Privacidade do CertBox

Atualização: 17 de novembro de 2026.

O CertBox processa certificados e chaves localmente. O aplicativo não envia os arquivos, seus conteúdos ou as senhas para a internet. Não exige conta e não inclui anúncios, telemetria, análise de uso ou consulta automática de atualizações.

Os arquivos escolhidos e as senhas informadas são usados na memória para inspeção, validação do par certificado/chave e exportação. Os arquivos exportados são gravados nos destinos escolhidos pelo usuário. O CertBox não instala certificados no sistema.

O único dado persistido nas configurações é o idioma, em %LOCALAPPDATA%\CertBox\settings.json. No Linux, o mesmo dado fica em $XDG_CONFIG_HOME/CertBox/settings.json ou ~/.config/CertBox/settings.json. Certificados, chaves, senhas e histórico de arquivos não são salvos nesse arquivo. Apagar esse arquivo restaura a escolha inicial de idioma.

O salvamento usa arquivos temporários no diretório de destino e, quando necessário, cópias de segurança transitórias para restaurar saídas em caso de erro. Esses arquivos são removidos quando a operação termina normalmente. Uma interrupção abrupta do processo ou do computador pode deixar arquivos transitórios no destino. O runtime também pode extrair suas próprias bibliotecas para uma pasta de cache de execução; essa extração não contém os certificados ou as chaves carregados.

Ao clicar nos links do projeto, o GitHub será aberto no navegador padrão, sujeito à política de privacidade desse site. O CertBox não envia seus certificados, chaves ou senhas ao GitHub. Para relatar problemas, utilize arquivos fictícios e mantenha seus dados reais em sigilo.

O código, a interface e o ícone foram produzidos com uso de IA durante o desenvolvimento. O CertBox não utiliza serviços de IA durante seu funcionamento e não envia os dados dos certificados para esses serviços. As bibliotecas de terceiros mantêm sua autoria e suas licenças.

Para dúvidas ou solicitações, utilize a página de suporte do repositório oficial indicada na seção Sobre do aplicativo e na página da versão distribuída.
