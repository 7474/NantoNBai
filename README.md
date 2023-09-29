# NantoNBai

私はこのスライドに示されているグラフを愛しています。

https://speakerdeck.com/papix/hatena-engineer-seminar-number-10?slide=52

>![image](https://github.com/7474/NantoNBai/assets/4744735/f3f4772c-ebcc-4ff3-a1e5-a10c7dff9166)

常々気軽にこのようなグラフを作成したいと思っていました。

思いが結実したのがこのリポジトリとAzure Functionsアプリです。


## Usage

以下のようなURLをゲットします。

`https://nanto-n-bai-w-f3eub2erhwekfde2.z01.azurefd.net/api/Generate.png?name=ポート番号&from=80&to=443`

>![image](https://nanto-n-bai-w-f3eub2erhwekfde2.z01.azurefd.net/api/Generate.png?name=ポート番号&from=80&to=443)

再現度はあまり高くないのでもうすよし「ヨセ」たいと考えています。


## API Spec

https://nanto-n-bai-w-f3eub2erhwekfde2.z01.azurefd.net/api/swagger/ui

残念ながら上記のUIでは正しいリクエストを発行できません。感じてください。


## Achitecture

このようなグラフの良いところの一つには、オフィスソフトで「雑に」作られたグラフであるところがあります。

その魅力をスポイルしてはなりません。

そのため、グラフはPowerPoint互換で生成しました。

これを適当にWebブラウザで表示できる形式に変換しました。

当初は低レイヤな OpenXML SDK を用いて生成を試みていましたが、異様に難解だったので諦めました。

ありがとう [ShapeCrawler](https://github.com/ShapeCrawler/ShapeCrawler)。

OpenXML はデータフォーマットなだけで、これによってpptxファイルを生成できても、画像データにはなりません。

ありがとう [Spire.Presentation（無料版）](https://jp.e-iceblue.com/download/free-spire-presentation-for-net.html)。
