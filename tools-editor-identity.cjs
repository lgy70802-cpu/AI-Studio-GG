const assert = require('node:assert/strict');
const fs = require('node:fs');
const { connect } = require('./tools-theme-common.cjs');
(async () => {
  const {browser,page,req}=await connect(9230);
  try {
    const lib=await req('library-load');
    const base=structuredClone(lib.themes.find(t=>t.editorProject));
    const origin='qa-identity-original';
    const original={...base,id:origin,displayName:'验收_标识(2)',name:'验收_标识(2)',source:'local',category:'壁纸',imageUrl:'https://media.studio.local/'+fs.readdirSync('artifacts/capture-qa/Library').find(n=>n.startsWith('capture-')&&n.endsWith('.png')),sourceWidth:800,sourceHeight:300,resolution:'1280×480',orientation:'landscape',editorProject:undefined};
    const other={...base,id:'custom-'+origin,displayName:'验收_标识(333)',name:'验收_标识(333)',editorProject:{...base.editorProject,template_id:origin,name:'旧的工程名称'}};
    lib.themes=lib.themes.filter(t=>!t.displayName.startsWith('验收_标识'));
    lib.themes.unshift(original,other);
    await req('library-save',{snapshot:lib});
    const untouched=JSON.stringify((await req('library-load')).themes.find(t=>t.id===other.id));
    await page.reload();
    await page.getByRole('button',{name:'资源库',exact:true}).click();
    if(await page.getByRole('button',{name:'显示同向',exact:true}).count()) await page.getByRole('button',{name:'显示同向',exact:true}).click();
    if(await page.getByRole('button',{name:'显示全部',exact:true}).count()) await page.getByRole('button',{name:'显示全部',exact:true}).click();
    async function open(name){
      const card=page.getByTitle(new RegExp('点击应用「'+name.replace(/[.*+?^${}()|[\]\\]/g,'\\$&')+'」')).locator('..');
      await card.hover();
      await card.getByLabel('在主题编辑器打开',{exact:true}).click();
    }
    await open(original.displayName);
    assert.equal(await page.getByLabel('主题名称',{exact:true}).inputValue(),original.displayName);
    await page.getByText('标题文字',{exact:true}).click();
    await page.getByLabel('主题名称',{exact:true}).fill('验收_标识(2)_保存');
    await page.getByRole('button',{name:'保存',exact:true}).click();
    await page.waitForTimeout(600);
    let saved=(await req('library-load')).themes.find(t=>t.displayName==='验收_标识(2)_保存');
    assert(saved);assert.notEqual(saved.id,other.id);assert.notEqual(saved.id,original.id);
    assert.equal(JSON.stringify((await req('library-load')).themes.find(t=>t.id===other.id)),untouched);
    const savedId=saved.id;
    await page.getByLabel('主题名称',{exact:true}).fill('验收_标识(2)_再次保存');
    await page.getByRole('button',{name:'保存',exact:true}).click();await page.waitForTimeout(600);
    assert.equal((await req('library-load')).themes.find(t=>t.displayName==='验收_标识(2)_再次保存').id,savedId);
    await page.getByRole('button',{name:'返回',exact:true}).click();
    await open(other.displayName);
    assert.equal(await page.getByLabel('主题名称',{exact:true}).inputValue(),other.displayName);
    await page.getByRole('button',{name:'保存',exact:true}).click();await page.waitForTimeout(600);
    const final=(await req('library-load')).themes;
    assert.equal(final.find(t=>t.id===other.id).editorProject.name,other.displayName);
    assert.equal(final.find(t=>t.id===savedId).displayName,'验收_标识(2)_再次保存');
    console.log('PASS same template ID saves independently, repeated saves keep ID, renamed project opens with resource name');
    fs.writeFileSync('artifacts/editor-identity-result.json',JSON.stringify({independentNewId:true,otherThemeUnchanged:true,repeatedSaveStable:true,renamedTitleCorrect:true},null,2));
  } finally { await browser.close(); }
})().catch(e=>{console.error(e);process.exit(1)});
