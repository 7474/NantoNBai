# NantoNBai

私はこのスライドに示されているグラフを愛しています。

https://speakerdeck.com/papix/hatena-engineer-seminar-number-10?slide=52

>![image](https://github.com/7474/NantoNBai/assets/4744735/4f88a511-e351-457d-8c49-8bbb66d6be08)


常々気軽にこのようなグラフを作成したいと思っていました。

思いが結実したのがこのリポジトリとAzure Functionsアプリです。


## Usage

以下のようなURLをゲットします。

`https://nanto-n-bai-w-f3eub2erhwekfde2.z01.azurefd.net/api/Generate.png?name=ポート番号&from=80&to=443`

>![image](https://nanto-n-bai-w-f3eub2erhwekfde2.z01.azurefd.net/api/Generate.png?name=ポート番号&from=80&to=443)

再現度はあまり高くないのでもうすよし「ヨセ」たいと考えています。


## API Spec

https://nanto-n-bai-w-f3eub2erhwekfde2.z01.azurefd.net/api/swagger/ui


## Achitecture?

このようなグラフの良いところの一つには、オフィスソフトで「雑に」作られたグラフであるところがあります。

その魅力をスポイルしてはなりません。

そのため、グラフはPowerPoint互換で生成し、適当にWebブラウザで表示できる形式に変換しました。

当初は低レイヤな OpenXML SDK を用いて生成を試みていましたが、[異様に難解](https://learn.microsoft.com/ja-jp/office/open-xml/working-with-presentations)だったので諦めました。

ありがとう [ShapeCrawler](https://github.com/ShapeCrawler/ShapeCrawler)。

OpenXML はデータフォーマットなだけで、これによってpptxファイルを生成できても、画像データにはなりません。

ありがとう [Spire.Presentation（無料版）](https://jp.e-iceblue.com/download/free-spire-presentation-for-net.html)。
