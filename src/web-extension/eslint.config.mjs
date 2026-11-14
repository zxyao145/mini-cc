import parser from '@typescript-eslint/parser';
import typescript from '@typescript-eslint/eslint-plugin';

export default [{
  files: ['src/**/*.ts'],
  languageOptions: { parser },
  plugins: { '@typescript-eslint': typescript },
  rules: typescript.configs.recommended.rules,
}];
