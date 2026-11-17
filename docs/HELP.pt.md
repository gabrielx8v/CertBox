# O que o CertBox verifica

O CertBox inspeciona certificados X.509 e exporta certificados e chaves em CRT, PEM e KEY. O processamento é local e os arquivos de entrada são protegidos contra sobrescrita pelas operações de exportação.

A indicação de prazo compara as datas do certificado com o relógio do computador. Ela não confirma confiança pública, correspondência com um site, revogação ou validade completa para uso em TLS. A ordenação da cadeia usa somente os certificados fornecidos; o programa não consulta nem baixa emissores pela internet.

As caixas de seleção controlam o conteúdo exportado. O seletor do painel de dados altera apenas o certificado visualizado. Todas as caixas começam desmarcadas.

Exportar CRT salva somente os certificados marcados, sem chave privada. Gerar PEM também inclui a chave carregada quando o certificado do domínio está marcado, após verificar o par. Salvar chave grava somente a chave. A criptografia de chaves protegidas é preservada; gerar um PEM com chave protegida exige a senha para verificar o par.

Certificados PEM/DER e chaves RSA, EC e DSA são aceitos nos formatos documentados no README. PFX/P12, PKCS#7, OpenSSH, Ed25519 e PEM com criptografia antiga Proc-Type não são suportados. Os limites são 32 arquivos de certificados, uma chave, 128 certificados e 16 MB por arquivo.

Arquivos CRT e KEY também podem conter texto PEM; a extensão indica a finalidade habitual e não garante a codificação. O programa identifica o conteúdo do arquivo.
