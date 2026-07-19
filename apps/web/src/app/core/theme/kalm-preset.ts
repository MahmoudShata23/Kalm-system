import { definePreset } from "@primeuix/themes";
import Aura from "@primeuix/themes/aura";

export const KalmPreset = definePreset(Aura, {
  primitive: {
    green: {
        50: "#f5f7f4",
        100: "#e7ece5",
        200: "#d3ddd0",
        300: "#b8c8b4",
        400: "#9ead98",
        500: "#8f9f8c",
        600: "#71826e",
        700: "#596756",
        800: "#485346",
        900: "#3c453a",
        950: "#20261f"
    },
    stone: {
        50: "#fdfbf8",
        100: "#f7f2ea",
        200: "#eee4d5",
        300: "#e6d6bf",
        400: "#d8c3a5",
        500: "#c8b596",
        600: "#ac9674",
        700: "#8b7658",
        800: "#715f49",
        900: "#5d4f3e",
        950: "#31291f"
    },
    neutral: {
        50: "#f8f6f4",
        100: "#eee9e5",
        200: "#ded4cc",
        300: "#c8b8ac",
        400: "#ad9686",
        500: "#957a68",
        600: "#7b6253",
        700: "#644f44",
        800: "#534239",
        900: "#3a2a22",
        950: "#1f1b18"
    },
    borderRadius: {
      none: "0",
      xs: "0.25rem",
      sm: "0.375rem",
      md: "0.5rem",
      lg: "0.75rem",
      xl: "1rem"
    }
  },
  semantic: {
    primary: {
      50: "{green.50}", 100: "{green.100}", 200: "{green.200}",
      300: "{green.300}", 400: "{green.400}", 500: "{green.500}",
      600: "{green.600}", 700: "{green.700}", 800: "{green.800}",
      900: "{green.900}", 950: "{green.950}"
    },
    surface: {
      0: "#ffffff", 50: "{stone.50}", 100: "{stone.100}",
      200: "{stone.200}", 300: "{stone.300}", 400: "{stone.400}",
      500: "{stone.500}", 600: "{stone.600}", 700: "{stone.700}",
      800: "{neutral.800}", 900: "{neutral.900}", 950: "{neutral.950}"
    },
    focusRing: {
      width: "3px",
      style: "solid",
      color: "{primary.700}",
      offset: "2px",
      shadow: "none"
    },
    formField: {
      paddingX: "0.875rem",
      paddingY: "0.75rem",
      borderRadius: "{border.radius.md}",
      focusBorderColor: "{primary.700}",
      transitionDuration: "0.15s"
    },
    colorScheme: {
      light: {
        primary: {
          color: "{neutral.950}",
          contrastColor: "{stone.50}",
          hoverColor: "{neutral.900}",
          activeColor: "{neutral.800}"
        },
        highlight: {
          background: "{green.100}",
          focusBackground: "{green.200}",
          color: "{neutral.950}",
          focusColor: "{neutral.950}"
        },
        formField: {
          background: "{surface.0}",
          disabledBackground: "{surface.100}",
          filledBackground: "{surface.50}",
          filledHoverBackground: "{surface.100}",
          filledFocusBackground: "{surface.0}",
          borderColor: "{stone.500}",
          hoverBorderColor: "{green.500}",
          color: "{neutral.950}",
          placeholderColor: "{neutral.600}",
          invalidPlaceholderColor: "{red.600}",
          floatLabelColor: "{neutral.600}",
          floatLabelFocusColor: "{primary.700}",
          floatLabelActiveColor: "{neutral.700}"
        }
      }
    }
  },
  components: {
    button: { root: { borderRadius: "{border.radius.md}", label: { fontWeight: "700" } } },
    dialog: { root: { borderRadius: "{border.radius.lg}" } },
    datatable: { headerCell: { background: "{surface.100}", color: "{neutral.950}" } },
    tabs: { tab: { activeColor: "{neutral.950}", activeBorderColor: "{primary.600}" } },
    tag: { root: { borderRadius: "{border.radius.sm}" } }
  }
});
