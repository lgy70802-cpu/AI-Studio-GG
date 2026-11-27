const fs = require('node:fs');
const path = require('node:path');
const {createRequire} = require('node:module');
const esbuild = createRequire(require.resolve('vite'))('esbuild');
async function main(){
 const html=fs.readFileSync('preview.source.html','utf8');
 const match=html.match(/<script type="module">([\s\S]*?)<\/script>/);
 if(!match)throw Error('Missing preview entry');
 const result=await esbuild.build({stdin:{contents:match[1].replace("'/src/stocks.ts'","'./src/stocks.ts'"),resolveDir:process.cwd(),loader:'ts'},bundle:true,write:false,format:'iife',target:'es2020'});
 fs.writeFileSync('preview.html',html.replace(match[0],()=>'<script>'+result.outputFiles[0].text.replace(/<\/script/gi,'<\\/script')+'</script>'));
 console.log('Built standalone preview.html');
}
main().catch(e=>{console.error(e);process.exitCode=1});
