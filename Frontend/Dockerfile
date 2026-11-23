FROM nginx:alpine
COPY Frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY Frontend/ /usr/share/nginx/html/
RUN rm -f /usr/share/nginx/html/Dockerfile /usr/share/nginx/html/nginx.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
