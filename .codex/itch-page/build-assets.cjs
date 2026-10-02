const fs = require('fs');
const path = require('path');
const sharp = require('C:/Users/ENDERWIS/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = __dirname;
const media = path.join(root, 'media');
fs.mkdirSync(media, {recursive: true});
const original = path.resolve(root, '../verification/scare-v2-frames/0000.png');
fs.copyFileSync(original, path.join(media, '04-basement.png'));
const data = 'data:image/png;base64,' + fs.readFileSync(original).toString('base64');
function svg(w, h, banner) {
  const top = banner ? 98 : 100;
  const size = banner ? 90 : 128;
  return `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">
  <defs><linearGradient id="shade" x1="0" x2="0" y1="0" y2="1"><stop offset="0" stop-color="#090b0b" stop-opacity=".98"/><stop offset=".48" stop-color="#090b0b" stop-opacity=".15"/><stop offset="1" stop-color="#090b0b" stop-opacity=".72"/></linearGradient></defs>
  <rect width="100%" height="100%" fill="#090b0b"/>
  <image xlink:href="${data}" x="0" y="${banner?0:200}" width="${w}" height="${banner?h:h-200}" preserveAspectRatio="xMidYMid slice"/>
  <rect width="100%" height="100%" fill="url(#shade)"/>
  <g font-family="Georgia,serif" text-anchor="middle" fill="#ece5d5">
  <text x="${w/2}" y="${top}" font-size="${size}" letter-spacing="8">KNOCKS</text>
  <text x="${w/2}" y="${top+size*.84}" font-size="${size*.55}" letter-spacing="5">FROM BENEATH</text>
  </g><path d="M${w*.37} ${top+size*1.12}h${w*.26}" stroke="#c59965" stroke-width="2"/>
  <text x="${w/2}" y="${h-62}" fill="#d9c6aa" text-anchor="middle" font-family="Georgia,serif" font-size="${banner?24:35}" letter-spacing="1">You moved. The knocking followed.</text>
  </svg>`;
}
(async()=>{
  for (const [name,w,h,b] of [['cover',1260,1000,false],['banner',1600,600,true]]) {
    const source=svg(w,h,b);
    fs.writeFileSync(path.join(media,name+'.svg'),source);
    await sharp(Buffer.from(source)).png().toFile(path.join(media,name+'.png'));
  }
  const description=fs.readFileSync(path.join(root,'description-en.html'),'utf8');
  const html=`<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Knocks from Beneath — page preview</title><style>
  *{box-sizing:border-box}body{margin:0;background:#0b0c0c;color:#e3ded2;font-family:Georgia,serif;line-height:1.65}.notice{padding:8px 20px;background:#242321;color:#dca66a;font:13px system-ui;text-align:center}main{max-width:960px;margin:36px auto;background:#151615;border:1px solid #2a2b27}.banner{width:100%;display:block}.intro{padding:40px 70px 10px}.intro p:first-child{font-size:27px;line-height:1.35}.intro h2{margin-top:38px;color:#dca66a;font-size:23px}.shots{padding:0 70px 45px}.shots img{display:block;width:100%;margin-top:24px;border:1px solid #363730}.shots figcaption{font:13px system-ui;color:#aaa394;margin-top:7px}.shots figure{margin:0}.label{display:inline-block;padding:5px 10px;border:1px solid #53422f;color:#dca66a;font:12px system-ui;letter-spacing:2px}.gallery-title{color:#dca66a;font-size:23px}footer{padding:24px 70px;border-top:1px solid #33342d;font:13px system-ui;color:#b5ad9f}@media(max-width:640px){main{margin:0;border:0}.intro,.shots{padding-left:24px;padding-right:24px}footer{padding:24px}.intro p:first-child{font-size:23px}}
  </style><div class="notice">LOCAL PREVIEW · This page has not yet been created on itch.io.</div><main><img class="banner" src="media/banner.png" alt="Knocks from Beneath — a flashlight illuminates the basement stairs"><div class="intro"><span class="label">FIRST-PERSON HORROR · IN DEVELOPMENT</span>${description}</div><section class="shots"><h2 class="gallery-title">Inside the house</h2>${[['01-home.png','A new home.'],['02-kitchen.png','Finish the ordinary things first.'],['03-bedroom.png','Try to get some sleep.'],['04-basement.png','The sound comes from below.']].map(([file,caption])=>`<figure><img src="media/${file}" alt="${caption}"><figcaption>${caption}</figcaption></figure>`).join('')}</section><footer>Development imagery. No public build available yet. Release date to be announced.</footer></main></html>`;
  fs.writeFileSync(path.join(root,'preview.html'),html);
  console.log('Created cover.png, banner.png, 4 screenshots, page copy and responsive preview.');
})();
