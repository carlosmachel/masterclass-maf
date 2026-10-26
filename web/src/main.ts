import { createApp } from 'vue'
import './style.css'
import 'primeicons/primeicons.css'
import PrimeVue from 'primevue/config'
import { definePreset } from '@primeuix/themes'
import Aura from '@primeuix/themes/aura'
import { createPinia } from 'pinia'
import App from './App.vue'

// Stage: fixed dark theme (projector), in the masterclass deck's colors (AzureBrasil): ice on navy,
// square corners. The .p-dark class is already on <html>.
const IpeTheme = definePreset(Aura, {
  primitive: {
    borderRadius: { none: '0', xs: '0', sm: '0', md: '2px', lg: '2px', xl: '4px' },
  },
  semantic: {
    primary: {
      50: '#f3f8ff',
      100: '#e6f0ff',
      200: '#d3e5ff',
      300: '#c4ddff',
      400: '#b9d6ff',
      500: '#b9d6ff',
      600: '#8fb8f0',
      700: '#5f8fd6',
      800: '#3f6cb8',
      900: '#2c5cb1',
      950: '#1b3a78',
    },
    colorScheme: {
      dark: {
        surface: {
          0: '#ffffff',
          50: '#f3f6ff',
          100: '#dfe5fb',
          200: '#c3cdef',
          300: '#a5b1e0',
          400: '#8d98cc',
          500: '#6c76ad',
          600: '#4d5690',
          700: '#3a3f78',
          800: '#2f2a6a',
          900: '#271d5c',
          950: '#211450',
        },
        primary: {
          color: '#b9d6ff',
          contrastColor: '#211450',
          hoverColor: '#d3e5ff',
          activeColor: '#e6f0ff',
        },
      },
    },
  },
})

const app = createApp(App)
app.use(PrimeVue, {
  theme: {
    preset: IpeTheme,
    options: {
      darkModeSelector: '.p-dark',
    },
  },
})
app.use(createPinia())
app.mount('#app')
