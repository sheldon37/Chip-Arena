import {defineConfig} from 'vite';
export default defineConfig({server:{host:'0.0.0.0',port:4173,allowedHosts:true,proxy:{'/socket':{target:'ws://127.0.0.1:8788',ws:true}}},build:{target:'es2022',chunkSizeWarningLimit:1800}});
