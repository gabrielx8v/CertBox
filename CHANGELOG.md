# Histórico do CertBox

Desenvolvido com IA: o código, a interface e o ícone foram produzidos com uso
de IA. Bibliotecas externas mantêm sua autoria e suas licenças.

## 2.3.1 — atualização de dependências

- Avalonia 12.1.6 nas duas plataformas, com versão centralizada para manter
  Win32, X11 e os pacotes compartilhados sincronizados nas propostas do Dependabot.
- SDK de compilação .NET 11.0.100; aplicativo e runtime continuam em .NET 10.
- Inicialização explícita do HarfBuzz e migração do placeholder de senha para
  a API atual, preservando os três idiomas e o comportamento da interface.
- Lockfiles, inventário de dependências e avisos de terceiros atualizados.
- Verificação automatizada dos PRs com builds de Windows e Linux.
- Linux continua sem teste manual, conforme o aviso nos três READMEs.

## 2.3.0 — versão preparada para a primeira publicação

- Versões gráficas Windows x64 e Linux x64, com dependências e lockfiles separados.
- Linux distribuído em TAR.GZ com permissão de execução; não foi testado manualmente.
- Repositório oficial configurado: https://github.com/gabrielx8v/CertBox.
- GPLv3 ou posterior aplicada ao código próprio, com texto integral no programa.
- Sobre, privacidade e explicação do alcance das verificações nas configurações.
- Declaração de desenvolvimento com IA no rodapé, em português, inglês e espanhol.
- Endereço do projeto centralizado em project.json para projeto, fonte, suporte
  e versões, sem conexões automáticas em segundo plano.
- Versões do manifesto e do executável alinhadas.
- Documentação pública e preparação de entrega portátil sem assinatura pelo GitHub.
- Ícone de caixa com documentos PEM e chave.
- Engrenagem de configurações, três idiomas e preferência persistente por usuário.
- Avisos das dependências incorporados ao executável.
- Executável autossuficiente com trimming e compressão.
- Botões inferiores com larguras iguais e intervalo centralizado.
