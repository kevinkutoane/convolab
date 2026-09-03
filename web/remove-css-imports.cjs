const fs = require('fs');
const path = require('path');
const dir = 'src/pages';
fs.readdirSync(dir).forEach(file => {
  if (file.endsWith('.tsx')) {
    const filePath = path.join(dir, file);
    const content = fs.readFileSync(filePath, 'utf8');
    const newContent = content.replace(/^import "\.\.\/.*\.css";\r?\n/gm, '');
    if (content !== newContent) {
      fs.writeFileSync(filePath, newContent);
      console.log('Updated ' + file);
    }
  }
});
