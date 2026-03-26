cat <<EOF > tailwind.config.js
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./dist/*.html", "./src/**/*.js"],
  theme: {
    extend: {},
  },
  plugins: [],
}
EOF