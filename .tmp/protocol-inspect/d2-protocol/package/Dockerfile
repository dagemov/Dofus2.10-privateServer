FROM timbru31/java-node:11-alpine-jdk

RUN apk update && \
    apk add wget && \
    apk add bash && \
    apk add unzip && \
    apk add git

RUN wget -c https://github.com/jindrapetrik/jpexs-decompiler/releases/download/version14.4.0/ffdec_14.4.0.zip -O ffdec.zip && \
    unzip ffdec.zip -d ffdec

RUN git clone https://github.com/RBlanchet/D2.ProtocolBuilder.git

COPY . /D2.Protocol
WORKDIR /D2.Protocol
RUN npm install --prefix /D2.Protocol
RUN npm install --prefix /D2.ProtocolBuilder
